using System;
using System.IO;
using Content.Server._DeepLagoon.DiscordLink;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture]
[NonParallelizable]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class DiscordLinkStoreTests
{
    private string _directory = default!;
    private string _path = default!;
    private long _now;

    [SetUp]
    public void Setup()
    {
#if USE_SYSTEM_SQLITE
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
#else
        SQLitePCL.Batteries_V2.Init();
#endif
        _directory = Path.Combine(Path.GetTempPath(), "ss14-discord-link-" + Guid.NewGuid());
        _path = Path.Combine(_directory, "discord-links.db");
        _now = 10000;
    }

    [TearDown]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, true);
    }

    [Test]
    public void StaffGrantPersistsAcrossRestartAndRemoval()
    {
        var uid = Guid.NewGuid();
        using (var store = new DiscordLinkStore(_path))
            store.SetStaffGrant(uid, "trial", 17);
        using (var store = new DiscordLinkStore(_path))
        {
            Assert.That(store.StaffGrant(uid), Is.EqualTo(("trial", 17)));
            store.SetStaffGrant(uid, "admin", 18);
            Assert.That(store.StaffGrant(uid), Is.EqualTo(("admin", 18)));
            store.RemoveStaffGrant(uid);
        }
        using (var store = new DiscordLinkStore(_path))
            Assert.That(store.StaffGrant(uid), Is.Null);
    }

    [Test]
    public void BoostyTierDowngradesAndExpiresWhilePaletteSurvivesRestart()
    {
        const string discord = "1554565156657299597";
        var colors = new[] { 0x112233, 0x223344, 0x334455, 0x445566 };
        Guid uid;
        using (var store = new DiscordLinkStore(_path, () => _now))
        {
            store.SetSupporter(discord, true, 3, colors);
            uid = store.EnrollLauncher(discord).Uid;
            Assert.That(store.BoostyStatus(uid), Is.EqualTo((3, 900)));
            Assert.That(store.BoostRemaining(uid), Is.EqualTo(900));
            Assert.That(store.SupporterColors(), Is.EqualTo(colors));
        }
        using (var store = new DiscordLinkStore(_path, () => _now))
        {
            Assert.That(store.BoostyStatus(uid), Is.EqualTo((3, 900)));
            store.SetSupporter(discord, false, 1, colors);
            Assert.That(store.BoostyStatus(uid), Is.EqualTo((1, 900)));
            Assert.That(store.BoostRemaining(uid), Is.Zero);
            _now += 901;
            Assert.That(store.BoostyStatus(uid), Is.EqualTo((0, 0)));
            Assert.That(store.SupporterColors(), Is.EqualTo(colors));
            store.SetSupporter(discord, false, 2, colors);
            store.Unlink(discord, uid);
            Assert.That(store.BoostyStatus(uid), Is.EqualTo((0, 0)));
        }
    }

    [Test]
    public void InvalidSupporterPayloadCannotChangeExistingPrivileges()
    {
        using var store = new DiscordLinkStore(_path, () => _now);
        const string discord = "1554565156657299597";
        var uid = store.EnrollLauncher(discord).Uid;
        var colors = new[] { 1, 2, 3, 4 };
        store.SetSupporter(discord, false, 1, colors);
        Assert.That(() => store.SetSupporter(discord, true, 4, colors), Throws.ArgumentException);
        Assert.That(() => store.SetSupporter(discord, true, 3, new[] { -1, 2, 3, 4 }), Throws.ArgumentException);
        Assert.That(() => store.SetSupporter(discord, true, 3, new[] { 1 }), Throws.ArgumentException);
        Assert.That(store.BoostyStatus(uid), Is.EqualTo((1, 900)));
        Assert.That(store.BoostRemaining(uid), Is.Zero);
    }

    [Test]
    public void BoostLeaseFollowsDiscordLinkAndExpiresWithoutRenewal()
    {
        const string discord = "1554565156657299597";
        Guid uid;
        using (var store = new DiscordLinkStore(_path, () => _now))
        {
            store.SetBoost(discord, true); // Boost can arrive before account linking.
            uid = store.EnrollLauncher(discord).Uid;
            Assert.That(store.BoostRemaining(uid), Is.EqualTo(900));
        }
        using (var store = new DiscordLinkStore(_path, () => _now))
        {
            _now += 300;
            Assert.That(store.BoostRemaining(uid), Is.EqualTo(600));
            store.SetBoost(discord, true);
            _now += 901;
            Assert.That(store.BoostRemaining(uid), Is.Zero);
            store.SetBoost(discord, true);
            store.SetBoost(discord, false);
            Assert.That(store.BoostRemaining(uid), Is.Zero);
            store.SetBoost(discord, true);
            store.Unlink(discord, uid);
            Assert.That(store.BoostRemaining(uid), Is.Zero);
            store.RestoreDiscord(discord, uid, "Player");
            Assert.That(store.BoostRemaining(uid), Is.EqualTo(900));
            store.ReassignDiscord(discord, "1554565156657299598", uid, _now);
            Assert.That(store.BoostRemaining(uid), Is.Zero);
        }
    }

    [Test]
    public void LauncherEnrollmentPreservesUidAndCannotRecreateRevokedIdentity()
    {
        using var store = new DiscordLinkStore(_path, () => _now);
        var registered = store.EnrollLauncher("1554565156657299597");
        Assert.That(store.EnrollLauncher("1554565156657299597").Uid, Is.EqualTo(registered.Uid));
        Assert.That(store.IsLinked(registered.Uid), Is.True);
        store.Unlink("1554565156657299597", registered.Uid);
        Assert.That(() => store.EnrollLauncher("1554565156657299597"), Throws.TypeOf<DiscordLinkStore.LinkException>());
        var officialUid = Guid.NewGuid();
        var code = store.Issue(officialUid, "ExistingPlayer");
        store.Consume("1554565156657299598", code);
        Assert.That(store.EnrollLauncher("1554565156657299598").Uid, Is.EqualTo(officialUid));
        store.Unlink("1554565156657299598", officialUid);
        Assert.That(() => store.EnrollLauncher("1554565156657299598"), Throws.TypeOf<DiscordLinkStore.LinkException>());
    }

    [Test]
    public void ExplicitMergeCodePreservesCanonicalUidAndIsBoundToDiscord()
    {
        using var store = new DiscordLinkStore(_path, () => _now);
        const string discord = "1554565156657299597";
        var canonical = store.EnrollLauncher(discord);
        var official = Guid.NewGuid();
        var normal = store.Issue(official, "OfficialPlayer");
        Assert.That(store.IsMergeCode(normal), Is.False);
        _now += 31;
        var code = store.IssueMerge(official, "OfficialPlayer");
        Assert.That(store.IsMergeCode(code), Is.True);
        var plan = store.PrepareMerge(discord, code);
        Assert.That(plan.CanonicalUid, Is.EqualTo(canonical.Uid));
        Assert.That(plan.OfficialUid, Is.EqualTo(official));
        Assert.That(() => store.PrepareMerge("1554565156657299598", code), Throws.TypeOf<DiscordLinkStore.LinkException>());
        var merged = store.CompleteMerge(discord, code, plan);
        Assert.That(merged.Uid, Is.EqualTo(canonical.Uid));
        Assert.That(store.EnrollLauncher(discord).Username, Is.EqualTo("OfficialPlayer"));
        Assert.That(store.PrepareMerge(discord, code), Is.EqualTo(plan));
    }

    [Test]
    public void LauncherNamesUseShortDiscordNicknameAndNeverRenameExistingUid()
    {
        using var store = new DiscordLinkStore(_path, () => _now);
        var first = store.EnrollLauncher("1554565156657299597", "discord.nickname");
        var second = store.EnrollLauncher("1554565156657299598", "discord.nickname");
        Assert.That(first.Username, Is.EqualTo("@discord.nickname"));
        Assert.That(first.Username.Length, Is.LessThanOrEqualTo(32));
        Assert.That(second.Username, Is.Not.EqualTo(first.Username));
        Assert.That(second.Username, Is.EqualTo("@discord.nickname_2"));
        Assert.That(store.EnrollLauncher("1554565156657299597", "renamed_discord"), Is.EqualTo(first));
        var official = Guid.NewGuid();
        store.Consume("1554565156657299599", store.Issue(official, "OfficialName"));
        Assert.That(store.EnrollLauncher("1554565156657299599", "discord.nickname"), Is.EqualTo(new DiscordLinkStore.Link(official, "OfficialName")));
    }

    [Test]
    public void CodeIsSingleUseAndMappingSurvivesRestart()
    {
        var uid = Guid.NewGuid();
        string code;
        using (var store = new DiscordLinkStore(_path, () => _now))
        {
            code = store.Issue(uid, "Player");
            Assert.That(code, Has.Length.EqualTo(24));
            Assert.That(store.Consume("1554565156657299597", code).Uid, Is.EqualTo(uid));
            Assert.That(store.IsLinked(uid), Is.True);
            Assert.That(() => store.Consume("1554565156657299598", code), Throws.TypeOf<DiscordLinkStore.LinkException>());
            Assert.That(() => store.Issue(uid, "Player"), Throws.TypeOf<DiscordLinkStore.LinkException>());
        }
        using var reopened = new DiscordLinkStore(_path, () => _now);
        Assert.That(reopened.FindDiscord("1554565156657299597")!.Uid, Is.EqualTo(uid));
        Assert.That(() => reopened.Consume("1554565156657299597", code), Throws.TypeOf<DiscordLinkStore.LinkException>());
    }

    [Test]
    public void ExpiryRegenerationAndHashStorage()
    {
        using var store = new DiscordLinkStore(_path, () => _now);
        var uid = Guid.NewGuid();
        var old = store.Issue(uid, "Player");
        Assert.That(() => store.Issue(uid, "Player"), Throws.TypeOf<DiscordLinkStore.LinkException>());
        _now += 31;
        var current = store.Issue(uid, "Player");
        Assert.That(() => store.Consume("1554565156657299597", old), Throws.TypeOf<DiscordLinkStore.LinkException>());
        using (var db = new SqliteConnection("Data Source=" + _path))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT code_hash FROM discord_link_codes";
            Assert.That(command.ExecuteScalar(), Is.Not.EqualTo(current));
        }
        _now += 600;
        Assert.That(() => store.Consume("1554565156657299597", current), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(store.IsLinked(uid), Is.False);
    }

    [Test]
    public void AttemptsPersistAndExistingDiscordCannotBeOverwritten()
    {
        var uid = Guid.NewGuid();
        string code;
        using (var store = new DiscordLinkStore(_path, () => _now))
        {
            code = store.Issue(uid, "Player");
            for (var i = 0; i < 10; i++)
                Assert.That(() => store.Consume("1554565156657299597", "wrong"), Throws.TypeOf<DiscordLinkStore.LinkException>());
        }
        using var reopened = new DiscordLinkStore(_path, () => _now);
        var error = Assert.Throws<DiscordLinkStore.LinkException>(() => reopened.Consume("1554565156657299597", code));
        Assert.That(error!.Message, Is.EqualTo("rate_limited"));
        _now += 601;
        var fresh = reopened.Issue(uid, "Player");
        reopened.Consume("1554565156657299597", fresh);
        var otherUid = Guid.NewGuid();
        var otherCode = reopened.Issue(otherUid, "Other");
        Assert.That(() => reopened.Consume("1554565156657299597", otherCode), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(reopened.FindDiscord("1554565156657299597")!.Uid, Is.EqualTo(uid));
        Assert.That(reopened.Consume("1554565156657299598", otherCode).Uid, Is.EqualTo(otherUid));
    }
    [Test]
    public void AdminReassignmentPreservesUidAndHistory()
    {
        using var store = new DiscordLinkStore(_path, () => _now);
        const string old = "1554565156657299597";
        const string next = "1554565156657299598";
        var uid = store.EnrollLauncher(old).Uid;
        Assert.That(() => store.ReassignDiscord(old, next, Guid.NewGuid(), _now), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(store.ReassignDiscord(old, next, uid, _now).Uid, Is.EqualTo(uid));
        Assert.That(store.FindDiscord(old), Is.Null);
        Assert.That(() => store.EnrollLauncher(old), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(() => store.AssertCurrent(next, uid, _now), Throws.TypeOf<DiscordLinkStore.LinkException>());
        store.Unlink(next, uid);
        Assert.That(() => store.EnrollLauncher(next), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(() => store.RestoreDiscord(next, Guid.NewGuid(), "Player"), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(store.RestoreDiscord(next, uid, "Player").Uid, Is.EqualTo(uid));
        var another = store.EnrollLauncher("1554565156657299599");
        store.Unlink("1554565156657299599", another.Uid);
        Assert.That(() => store.ReassignDiscord(next, "1554565156657299599", uid, _now), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(store.FindDiscord(next)!.Uid, Is.EqualTo(uid));
    }

    [Test]
    public void UnlinkRequiresMatchingIdentityAndAllowsFreshLink()
    {
        var uid = Guid.NewGuid();
        using var store = new DiscordLinkStore(_path, () => _now);
        var code = store.Issue(uid, "Player");
        store.Consume("1554565156657299597", code);
        Assert.That(() => store.Unlink("1554565156657299597", Guid.NewGuid()), Throws.TypeOf<DiscordLinkStore.LinkException>());
        Assert.That(store.IsLinked(uid), Is.True);
        store.Unlink("1554565156657299597", uid);
        Assert.That(store.IsLinked(uid), Is.False);
        Assert.That(store.FindDiscord("1554565156657299597"), Is.Null);
        Assert.That(() => store.Consume("1554565156657299597", code), Throws.TypeOf<DiscordLinkStore.LinkException>());
        store.Consume("1554565156657299598", store.Issue(uid, "Player"));
        Assert.That(store.FindDiscord("1554565156657299598")!.Uid, Is.EqualTo(uid));
    }

    [Test]
    public void RapidRestorationInvalidatesOldEditorRevision()
    {
        using var store = new DiscordLinkStore(_path, () => _now);
        const string discord = "1554565156657299597";
        var uid = store.EnrollLauncher(discord).Uid;
        store.AssertCurrent(discord, uid, _now, 1);
        store.Unlink(discord, uid);
        store.RestoreDiscord(discord, uid, "Player");
        Assert.That(() => store.AssertCurrent(discord, uid, _now, 1), Throws.TypeOf<DiscordLinkStore.LinkException>());
        store.AssertCurrent(discord, uid, _now, 3);
    }
}
