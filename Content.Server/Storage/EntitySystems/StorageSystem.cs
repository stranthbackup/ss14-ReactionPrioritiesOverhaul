using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Lock;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Players;
using Robust.Shared.Utility;

namespace Content.Server.Storage.EntitySystems;

public sealed class StorageSystem : SharedStorageSystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StorageComponent, GetVerbsEvent<ActivationVerb>>(AddOpenUiVerb);
        SubscribeLocalEvent<StorageComponent, BoundUIClosedEvent>(OnBoundUIClosed);
    }

    private void AddOpenUiVerb(EntityUid uid, StorageComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        var silent = false;
        if (!args.CanAccess || !args.CanInteract || TryComp<LockComponent>(uid, out var lockComponent) && lockComponent.Locked)
        {
            // we allow admins to open the storage anyways
            if (!_admin.HasAdminFlag(args.User, AdminFlags.Admin))
                return;

            silent = true;
        }

        silent |= HasComp<GhostComponent>(args.User);

        // Get the session for the user
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        // Does this player currently have the storage UI open?
        var uiOpen = _uiSystem.SessionHasOpenUi(uid, StorageComponent.StorageUiKey.Key, actor.PlayerSession);

        ActivationVerb verb = new()
        {
            Act = () => OpenStorageUI(uid, args.User, component, silent)
        };
        if (uiOpen)
        {
            verb.Text = Loc.GetString("verb-common-close-ui");
            verb.Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/close.svg.192dpi.png"));
        }
        else
        {
            verb.Text = Loc.GetString("verb-common-open-ui");
            verb.Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"));
        }
        args.Verbs.Add(verb);
    }

    private void OnBoundUIClosed(EntityUid uid, StorageComponent storageComp, BoundUIClosedEvent args)
    {
        if (TryComp<ActorComponent>(args.Session.AttachedEntity, out var actor) && actor?.PlayerSession != null)
            CloseNestedInterfaces(uid, actor.PlayerSession, storageComp);

        // If UI is closed for everyone
        if (!_uiSystem.IsUiOpen(uid, args.UiKey))
        {
            storageComp.IsUiOpen = false;
            UpdateStorageVisualization(uid, storageComp);

            if (storageComp.StorageCloseSound is not null)
                _audio.Play(storageComp.StorageCloseSound, Filter.Pvs(uid, entityManager: EntityManager), uid, true, storageComp.StorageCloseSound.Params);
        }
    }

    /// <summary>
    ///     Opens the storage UI for an entity
    /// </summary>
    /// <param name="entity">The entity to open the UI for</param>
    public void OpenStorageUI(EntityUid uid, EntityUid entity, StorageComponent? storageComp = null, bool silent = false)
    {
        if (!Resolve(uid, ref storageComp) || !TryComp(entity, out ActorComponent? player))
            return;

        // prevent spamming bag open / honkerton honk sound
        silent |= TryComp<UseDelayComponent>(uid, out var useDelay) && _useDelay.ActiveDelay(uid, useDelay);
        if (!silent)
        {
            _audio.PlayPvs(storageComp.StorageOpenSound, uid);
            if (useDelay != null)
                _useDelay.BeginDelay(uid, useDelay);
        }

        Log.Debug($"Storage (UID {uid}) \"used\" by player session (UID {player.PlayerSession.AttachedEntity}).");

        var bui = _uiSystem.GetUiOrNull(uid, StorageComponent.StorageUiKey.Key);
        if (bui != null)
            _uiSystem.OpenUi(bui, player.PlayerSession);
    }

    /// <summary>
    ///     If the user has nested-UIs open (e.g., PDA UI open when pda is in a backpack), close them.
    /// </summary>
    /// <param name="session"></param>
    public void CloseNestedInterfaces(EntityUid uid, IPlayerSession session, StorageComponent? storageComp = null)
    {
        if (!Resolve(uid, ref storageComp))
            return;

        // for each containing thing
        // if it has a storage comp
        // ensure unsubscribe from session
        // if it has a ui component
        // close ui
        foreach (var entity in storageComp.Container.ContainedEntities)
        {
            if (!TryComp(entity, out ServerUserInterfaceComponent? ui))
                continue;

            foreach (var bui in ui.Interfaces.Values)
            {
                _uiSystem.TryClose(entity, bui.UiKey, session, ui);
            }
        }
    }

    public void UpdateStorageUI(EntityUid uid, StorageComponent storageComp)
    {
        var state = new StorageComponent.StorageBoundUserInterfaceState((List<EntityUid>) storageComp.Container.ContainedEntities, storageComp.StorageUsed, storageComp.StorageCapacityMax);
        var bui = _uiSystem.GetUiOrNull(uid, StorageComponent.StorageUiKey.Key);

        if (bui != null)
            UserInterfaceSystem.SetUiState(bui, state);
    }
}
