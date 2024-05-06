using Content.Shared.MouseRotator;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.MouseRotator;

/// <inheritdoc/>
public sealed class MouseRotatorSystem : SharedMouseRotatorSystem
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted || !_input.MouseScreenPosition.IsValid)
            return;

        var player = _player.LocalEntity;

        if (player == null || !TryComp<MouseRotatorComponent>(player, out var rotator))
            return;

        // Get mouse loc and convert to angle based on player location
        var coords = _input.MouseScreenPosition;
        var mapPos = _eye.PixelToMap(coords);

        if (mapPos.MapId == MapId.Nullspace)
            return;

        (var curPos, var curRot) = _transformSystem.GetWorldPositionRotation(player.Value);
        var angle = (mapPos.Position - curPos).ToWorldAngle();

        // 4-dir handling is separate --
        // only raise event if the cardinal direction has changed
        if (rotator.Simple4DirMode)
        {
            var angleDir = angle.GetCardinalDir();
            if (angleDir == curRot.GetCardinalDir())
                return;

            RaisePredictiveEvent(new RequestMouseRotatorRotationSimpleEvent()
            {
                Direction = angleDir,
            });

            return;
        }

        // Don't raise event if mouse ~hasn't moved (or if too close to goal rotation already)
        var diff = Angle.ShortestDistance(angle, curRot);
        if (Math.Abs(diff.Theta) < rotator.AngleTolerance.Theta)
            return;

        if (rotator.GoalRotation != null)
        {
            var goalDiff = Angle.ShortestDistance(angle, rotator.GoalRotation.Value);
            if (Math.Abs(goalDiff.Theta) < rotator.AngleTolerance.Theta)
                return;
        }

        RaisePredictiveEvent(new RequestMouseRotatorRotationEvent
        {
            Rotation = angle
        });
    }
}
