using Content.Shared.Popups;
using Content.Server.Power.Components;
using Content.Server.UserInterface;
using JetBrains.Annotations;
using Content.Shared.Wires;

namespace Content.Server.Power.EntitySystems;

[UsedImplicitly]
[InjectDependencies]
internal sealed partial class ActivatableUIRequiresPowerSystem : EntitySystem
{
    [Dependency] private ActivatableUISystem _activatableUI = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActivatableUIRequiresPowerComponent, ActivatableUIOpenAttemptEvent>(OnActivate);
        SubscribeLocalEvent<ActivatableUIRequiresPowerComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnActivate(EntityUid uid, ActivatableUIRequiresPowerComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled) return;
        if (this.IsPowered(uid, EntityManager)) return;
        if (TryComp<WiresPanelComponent>(uid, out var panel) && panel.Open)
            return;
        _popup.PopupCursor(Loc.GetString("base-computer-ui-component-not-powered", ("machine", uid)), args.User);
        args.Cancel();
    }

    private void OnPowerChanged(EntityUid uid, ActivatableUIRequiresPowerComponent component, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            _activatableUI.CloseAll(uid);
    }
}
