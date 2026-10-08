using Content.Shared.Examine;
using Content.Shared._DeepLagoon.CharacterInfo;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Content.Shared.IdentityManagement;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.DetailExaminable;

public sealed partial class DetailExaminableSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetailExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<DetailExaminableComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName)
            return;

        var detailsRange = _examine.IsInDetailsRange(args.User, ent);

        var user = args.User;

        var verb = new ExamineVerb
        {
            Act = () =>
            {
                if (ent.Comp.CharacterCard)
                {
                    // The verb already reaches the server as a predictive event.
                    // Client reconciliation replays its Act on subsequent ticks;
                    // sending another request here would repeatedly open the card.
                    if (IoCManager.Resolve<INetManager>().IsServer && TryComp<ActorComponent>(user, out var actor))
                        RaiseNetworkEvent(new CharacterInfoOpenEvent(GetNetEntity(ent)), actor.PlayerSession.Channel);
                    return;
                }
                var markup = new FormattedMessage();
                markup.AddMarkupPermissive(ent.Comp.Content);
                _examine.SendExamineTooltip(user, ent, markup, false, false);
            },
            Text = Loc.GetString("detail-examinable-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("detail-examinable-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }
}
