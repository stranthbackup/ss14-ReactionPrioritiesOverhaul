using Content.Shared.Interaction;
using Content.Shared.SubFloor;
using Robust.Shared.GameObjects;

namespace Content.Server.SubFloor;

public class TrayScannerSystem : SharedTrayScannerSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrayScannerComponent, UseInHandEvent>(OnTrayScannerUsed);
    }

    private void OnTrayScannerUsed(EntityUid uid, TrayScannerComponent scanner, UseInHandEvent args)
    {
        ToggleTrayScanner(uid, !scanner.Toggled, scanner);
        if (EntityManager.TryGetComponent<AppearanceComponent>(uid, out var appearance))
        {
            appearance.SetData(TrayScannerVisual.Visual, scanner.Toggled == true ? TrayScannerVisual.On : TrayScannerVisual.Off);
        }
    }
}
