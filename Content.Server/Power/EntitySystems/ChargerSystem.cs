using Content.Server.Power.Components;
using Content.Server.PowerCell;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.PowerCell.Components;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Content.Shared.Power;

namespace Content.Server.Power.EntitySystems;

[UsedImplicitly]
internal sealed class ChargerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly PowerCellSystem _cellSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _sharedAppearanceSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ChargerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ChargerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ChargerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ChargerComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ChargerComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ChargerComponent, ExaminedEvent>(OnChargerExamine);
    }

    private void OnStartup(EntityUid uid, ChargerComponent component, ComponentStartup args)
    {
        UpdateStatus(uid, component);
    }

    private void OnChargerExamine(EntityUid uid, ChargerComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("charger-examine", ("color", "yellow"), ("chargeRate", component.ChargeRate)));
    }

    public override void Update(float frameTime)
    {
        foreach (var comp in EntityManager.EntityQuery<ChargerActiveComponent>())
        {
            if (!TryComp(comp.Owner, out ChargerComponent? charger))
                return;

            if (!_itemSlotsSystem.TryGetSlot(comp.Owner, charger.SlotId, out ItemSlot? slot))
                continue;

            if (charger.Status == CellChargerStatus.Empty || charger.Status == CellChargerStatus.Charged || !slot.HasItem)
                continue;

            TransferPower(comp.Owner, charger, frameTime);
        }
    }
    
    private void OnPowerChanged(EntityUid uid, ChargerComponent component, ref PowerChangedEvent args)
    {
        UpdateStatus(uid, component);
    }

    private void OnInserted(EntityUid uid, ChargerComponent component, EntInsertedIntoContainerMessage args)
    {
        if (!component.Initialized)
            return;

        if (args.Container.ID != component.SlotId)
            return;

        AddComp<ChargerActiveComponent>(uid);
        UpdateStatus(uid, component);
    }

    private void OnRemoved(EntityUid uid, ChargerComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.SlotId)
            return;

        RemComp<ChargerActiveComponent>(uid);
        UpdateStatus(uid, component);
    }

    /// <summary>
    ///     Verify that the entity being inserted is actually rechargeable.
    /// </summary>
    private void OnInsertAttempt(EntityUid uid, ChargerComponent component, ContainerIsInsertingAttemptEvent args)
    {
        if (!component.Initialized)
            return;

        if (args.Container.ID != component.SlotId)
            return;

        if (!TryComp(args.EntityUid, out PowerCellSlotComponent? cellSlot))
            return;
        
        if (!_itemSlotsSystem.TryGetSlot(args.EntityUid, cellSlot.CellSlotId, out ItemSlot? itemSlot))
            return;

        if (!cellSlot.FitsInCharger || !itemSlot.HasItem)
            args.Cancel();
    }

    private void UpdateStatus(EntityUid uid, ChargerComponent component)
    {
        var status = GetStatus(uid, component);
        if (component.Status == status || !TryComp(uid, out ApcPowerReceiverComponent? receiver))
            return;

        if (!_itemSlotsSystem.TryGetSlot(uid, component.SlotId, out ItemSlot? slot))
            return;

        TryComp(uid, out AppearanceComponent? appearance);

        component.Status = status;
        
        switch (component.Status)
        {
            case CellChargerStatus.Off:
                receiver.Load = 0;
                _sharedAppearanceSystem.SetData(uid, CellVisual.Light, CellChargerStatus.Off, appearance);
                break;
            case CellChargerStatus.Empty:
                receiver.Load = 0;
                _sharedAppearanceSystem.SetData(uid, CellVisual.Light, CellChargerStatus.Empty, appearance);
                break;
            case CellChargerStatus.Charging:
                receiver.Load = component.ChargeRate;
                _sharedAppearanceSystem.SetData(uid, CellVisual.Light, CellChargerStatus.Charging, appearance);
                break;
            case CellChargerStatus.Charged:
                receiver.Load = 0;
                _sharedAppearanceSystem.SetData(uid, CellVisual.Light, CellChargerStatus.Charged, appearance);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _sharedAppearanceSystem.SetData(uid, CellVisual.Occupied, slot.HasItem, appearance);
    }

    private CellChargerStatus GetStatus(EntityUid uid, ChargerComponent component)
    {
        if (!TryComp(uid, out TransformComponent? transformComponent))
            return CellChargerStatus.Off;

        if (!transformComponent.Anchored)
            return CellChargerStatus.Off;

        if (!TryComp(uid, out ApcPowerReceiverComponent? apcPowerReceiverComponent))
            return CellChargerStatus.Off;

        if (!apcPowerReceiverComponent.Powered)
            return CellChargerStatus.Off;

        if (!_itemSlotsSystem.TryGetSlot(uid, component.SlotId, out ItemSlot? slot))
            return CellChargerStatus.Off;

        if (!_cellSystem.TryGetBatteryFromSlot(uid, out BatteryComponent? heldBattery))
            return CellChargerStatus.Off;

        if (!slot.HasItem)
            return CellChargerStatus.Empty;

        if (heldBattery != null && Math.Abs(heldBattery.MaxCharge - heldBattery.CurrentCharge) < 0.01)
            return CellChargerStatus.Charged;

        return CellChargerStatus.Charging;
    }

    private void TransferPower(EntityUid uid, ChargerComponent component, float frameTime)
    {
        if (!TryComp(uid, out ApcPowerReceiverComponent? receiverComponent))
            return;

        if (!receiverComponent.Powered)
            return;

        if (!_cellSystem.TryGetBatteryFromSlot(uid, out BatteryComponent? heldBattery))
            return;

        heldBattery.CurrentCharge += component.ChargeRate * frameTime;
        // Just so the sprite won't be set to 99.99999% visibility
        if (heldBattery.MaxCharge - heldBattery.CurrentCharge < 0.01)
        {
            heldBattery.CurrentCharge = heldBattery.MaxCharge;
        }

        UpdateStatus(uid, component);
    }
}
