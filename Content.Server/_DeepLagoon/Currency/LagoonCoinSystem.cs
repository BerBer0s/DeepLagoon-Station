using System.Linq;
using System.Threading.Tasks;
using Content.Server.Afk;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Objectives;
using Content.Shared._DeepLagoon.Currency;
using Content.Shared._DeepLagoon.DiscordLink;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._DeepLagoon.Currency;

/// <summary>A separate donation wallet. Gameplay bank deposits never reach this system.</summary>
public sealed class LagoonCoinSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ITaskManager _tasks = default!;
    [Dependency] private readonly IAfkManager _afk = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedDiscordBoostSystem _supporters = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    private readonly HashSet<NetUserId> _ready = new();
    private readonly HashSet<NetUserId> _spawned = new();
    private readonly Dictionary<ICommonSession, Participation> _participation = new();
    private readonly Queue<Reward> _pending = new();
    private readonly Dictionary<NetUserId, TimeSpan> _requests = new();
    private readonly string _run = Guid.NewGuid().ToString("N");
    private long _sequence;
    private bool _draining;
    private bool _roundEnded;
    private TimeSpan _nextSave;
    private TimeSpan _retryAt;
    private Task _drainTask = Task.CompletedTask;

    private sealed class Participation
    {
        public TimeSpan Last;
        public bool Active;
        public bool Subscribed;
        public long Played;
        public long Bonus;
    }

    private sealed record Reward(NetUserId User, string Key, long Amount, string Reason,
        NetUserId? Actor = null, long Played = 0, long Bonus = 0);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LagoonCoinBalanceRequest>(OnBalanceRequest);
        SubscribeLocalEvent<RoundStartingEvent>(_ => _spawned.Clear());
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawn);
        SubscribeLocalEvent<RoundStartedEvent>(OnStarted);
        SubscribeLocalEvent<RoundEndedEvent>(OnEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            SaveParticipation();
            _ready.Clear();
            _spawned.Clear();
        });
        _players.PlayerStatusChanged += OnStatusChanged;
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnStatusChanged;
        SaveParticipation();
        _tasks.BlockWaitOnTask(_drainTask);
        _drainTask = Drain();
        _tasks.BlockWaitOnTask(_drainTask);
        if (_pending.Count != 0)
            Log.Error($"Shutdown with {_pending.Count} uncommitted Lagoon Coin rewards; database is unavailable.");
        base.Shutdown();
    }

    public void SetReady(ICommonSession session, bool ready)
    {
        if (ready) _ready.Add(session.UserId);
        else _ready.Remove(session.UserId);
    }

    private void OnSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.LateJoin && !ev.Silent && _ready.Contains(ev.Player.UserId))
            _spawned.Add(ev.Player.UserId);
    }

    private void OnStarted(RoundStartedEvent ev)
    {
        _roundEnded = false;
        foreach (var user in _spawned)
            Queue(new Reward(user, $"round:{ev.RoundId}:ready", LagoonCoinRules.ReadyReward, "ready"));
        _spawned.Clear();
    }

    private void OnEnded(RoundEndedEvent ev)
    {
        SaveParticipation();
        _roundEnded = true;
        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (!_roles.MindIsAntagonist(mindId) || (mind.UserId ?? mind.OriginalOwnerUserId) is not { } user)
                continue;
            foreach (var objective in mind.Objectives.Distinct())
            {
                if (!EntityManager.EntityExists(objective) || _objectives.GetProgress(objective) is not > 0.99f)
                    continue;
                Queue(new Reward(user, $"round:{ev.RoundId}:objective:{objective}",
                    LagoonCoinRules.ObjectiveReward, "antagonist-objective"));
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var session in _players.Sessions)
            Accumulate(session);
        if (_timing.RealTime >= _nextSave)
        {
            _nextSave = _timing.RealTime + TimeSpan.FromMinutes(1);
            SaveParticipation();
        }
        if (!_draining && _pending.Count > 0 && _timing.RealTime >= _retryAt)
            _drainTask = Drain();
    }

    private void Accumulate(ICommonSession session)
    {
        var now = _timing.RealTime;
        if (!_participation.TryGetValue(session, out var data))
            _participation[session] = data = new Participation { Last = now };
        var elapsed = Math.Max(0, (now - data.Last).Ticks);
        if (data.Active)
        {
            data.Played = checked(data.Played + elapsed);
            if (data.Subscribed) data.Bonus = checked(data.Bonus + elapsed);
        }
        data.Last = now;
        var attached = session.AttachedEntity;
        data.Active = LagoonCoinRules.CountsTime(_ticker.RunLevel == GameRunLevel.InRound && !_roundEnded,
            session.Status == SessionStatus.InGame, attached != null,
            HasComp<GhostComponent>(attached),
            TryComp<MobStateComponent>(attached, out var mob) && mob.CurrentState is MobState.Alive or MobState.Critical,
            _afk.IsAfk(session));
        data.Subscribed = LagoonCoinRules.HasSubscription(_supporters.GetBoostyTier(session));
    }

    private void SaveParticipation()
    {
        foreach (var (session, data) in _participation.ToArray())
        {
            Accumulate(session);
            Flush(session, data);
        }
    }

    private void Flush(ICommonSession session, Participation data)
    {
        if (data.Played == 0) return;
        Queue(new Reward(session.UserId, $"time:{_run}:{++_sequence}", 0, "playtime", Played: data.Played, Bonus: data.Bonus));
        data.Played = data.Bonus = 0;
    }

    private void OnStatusChanged(object? sender, SessionStatusEventArgs ev)
    {
        if (ev.NewStatus != SessionStatus.Disconnected) return;
        Accumulate(ev.Session);
        if (_participation.Remove(ev.Session, out var data)) Flush(ev.Session, data);
        _requests.Remove(ev.Session.UserId);
    }

    private void Queue(Reward reward) => _pending.Enqueue(reward);

    private async Task Drain()
    {
        if (_draining) return;
        _draining = true;
        try
        {
            while (_pending.TryPeek(out var reward))
            {
                try
                {
                    var result = await _db.AwardLagoonCoinsAsync(reward.User, reward.Key, reward.Amount,
                        reward.Reason, reward.Actor, reward.Played, reward.Bonus);
                    _pending.Dequeue();
                    SendBalance(reward.User, result.Balance);
                }
                catch (Exception e)
                {
                    Log.Error($"Lagoon Coin reward {reward.Key} not committed; retrying the same key: {e}");
                    _retryAt = _timing.RealTime + TimeSpan.FromSeconds(30);
                    break;
                }
            }
        }
        finally { _draining = false; }
    }

    public async Task<LagoonCoinResult> Grant(NetUserId user, long amount, string reason, NetUserId? actor)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var result = await _db.AwardLagoonCoinsAsync(user, $"admin:{Guid.NewGuid():N}", amount, reason, actor);
        SendBalance(user, result.Balance);
        return result;
    }

    private void SendBalance(NetUserId user, long balance)
    {
        if (_players.TryGetSessionById(user, out var session) && session.Channel.IsConnected)
            RaiseNetworkEvent(new LagoonCoinBalanceEvent(balance), session);
    }

    private async void OnBalanceRequest(LagoonCoinBalanceRequest ev, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.UserId;
        if (_requests.TryGetValue(user, out var last) && _timing.RealTime - last < TimeSpan.FromSeconds(1)) return;
        _requests[user] = _timing.RealTime;
        try { SendBalance(user, await _db.GetLagoonCoinsAsync(user)); }
        catch (Exception e) { Log.Error($"Could not fetch Lagoon Coin balance for {user}: {e}"); }
    }
}
