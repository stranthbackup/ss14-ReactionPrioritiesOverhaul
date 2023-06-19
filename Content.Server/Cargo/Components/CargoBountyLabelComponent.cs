﻿namespace Content.Server.Cargo.Components;

/// <summary>
/// This is used for marking containers as
/// containing goods for fulfilling bounties.
/// </summary>
[RegisterComponent]
public sealed class CargoBountyLabelComponent : Component
{
    /// <summary>
    /// The ID for the bounty this label corresponds to.
    /// </summary>
    [DataField("id"), ViewVariables(VVAccess.ReadWrite)]
    public int Id;
}
