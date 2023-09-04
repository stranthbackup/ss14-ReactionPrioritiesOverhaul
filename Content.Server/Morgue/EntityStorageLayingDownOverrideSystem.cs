using Content.Server.Morgue.Components;
using Content.Shared.Body.Components;
using Content.Shared.Standing;
using Content.Shared.Storage.Components;

namespace Content.Server.Morgue;

[InjectDependencies]
public sealed partial class EntityStorageLayingDownOverrideSystem : EntitySystem
{
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityStorageLayingDownOverrideComponent, StorageBeforeCloseEvent>(OnBeforeClose);
    }

    private void OnBeforeClose(EntityUid uid, EntityStorageLayingDownOverrideComponent component, ref StorageBeforeCloseEvent args)
    {
        foreach (var ent in args.Contents)
        {
            if (HasComp<BodyComponent>(ent) && !_standing.IsDown(ent))
                args.Contents.Remove(ent);
        }
    }
}
