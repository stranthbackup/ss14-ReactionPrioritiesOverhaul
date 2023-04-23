﻿using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Pinpointer;

/// <summary>
/// This is used for an item that beeps based on
/// proximity to a specified component.
/// </summary>
[RegisterComponent]
public sealed class ProximityBeeperComponent : Component
{
    /// <summary>
    /// The target component that is being searched for
    /// </summary>
    [DataField("component", required: true), ViewVariables(VVAccess.ReadWrite)]
    public string Component = default!;

    /// <summary>
    /// The farthest distance a target can be for the beep to occur
    /// </summary>
    [DataField("maximumDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float MaximumDistance = 10f;

    /// <summary>
    /// The maximum interval between beeps.
    /// </summary>
    [DataField("maxBeepInterval"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan MaxBeepInterval = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// The minimum interval between beeps.
    /// </summary>
    [DataField("minBeepInterval"), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan MinBeepInterval = TimeSpan.FromSeconds(0.25f);

    [DataField("nextBeepTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextBeepTime;

    [DataField("beepSound")]
    public SoundSpecifier? BeepSound;
}

/// <summary>
/// Active tracking comp for <see cref="ProximityBeeperComponent"/>
/// </summary>
[RegisterComponent]
public sealed class ActiveProximityBeeperComponent : Component
{

}
