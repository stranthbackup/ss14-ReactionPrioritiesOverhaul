using Content.Server.Chemistry.Components;
using Content.Server.Friends.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;

namespace Content.Server.Friends.Systems;

[InjectDependencies]
public sealed partial class PettableFriendSystem : EntitySystem
{
    [Dependency] private FactionExceptionSystem _factionException = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PettableFriendComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<PettableFriendComponent, GotRehydratedEvent>(OnRehydrated);
    }

    private void OnUseInHand(EntityUid uid, PettableFriendComponent comp, UseInHandEvent args)
    {
        var user = args.User;
        if (args.Handled || !TryComp<FactionExceptionComponent>(uid, out var factionException))
            return;

        if (_factionException.IsIgnored(factionException, user))
        {
            _popup.PopupEntity(Loc.GetString(comp.FailureString, ("target", uid)), user, user);
            return;
        }

        // you have made a new friend :)
        _popup.PopupEntity(Loc.GetString(comp.SuccessString, ("target", uid)), user, user);
        _factionException.IgnoreEntity(factionException, user);
        args.Handled = true;
    }

    private void OnRehydrated(EntityUid uid, PettableFriendComponent _, ref GotRehydratedEvent args)
    {
        // can only pet before hydrating, after that the fish cannot be negotiated with
        if (!TryComp<FactionExceptionComponent>(uid, out var comp))
            return;

        var targetComp = AddComp<FactionExceptionComponent>(args.Target);
        _factionException.IgnoreEntities(targetComp, comp.Ignored);
    }
}
