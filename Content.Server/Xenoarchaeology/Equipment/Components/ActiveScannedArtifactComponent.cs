﻿using Robust.Shared.Audio;

namespace Content.Server.Xenoarchaeology.Equipment.Components;

/// <summary>
/// This is used for tracking artifacts that are currently
/// being scanned by <see cref="ActiveArtifactAnalyzerComponent"/>
/// </summary>
[RegisterComponent]
public sealed class ActiveScannedArtifactComponent : Component
{
    [ViewVariables]
    public EntityUid Scanner;

    public readonly SoundSpecifier ScanFailureSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
}
