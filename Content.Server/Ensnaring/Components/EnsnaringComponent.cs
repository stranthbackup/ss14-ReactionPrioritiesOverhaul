﻿using System.Threading;
using Content.Shared.Ensnaring.Components;

namespace Content.Server.Ensnaring.Components;
[RegisterComponent]
[ComponentReference(typeof(SharedEnsnaringComponent))]
public sealed class EnsnaringComponent : SharedEnsnaringComponent
{
    /// <summary>
    /// Should movement cancel breaking out?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("canMoveBreakout")]
    public bool CanMoveBreakout;

    public CancellationTokenSource? CancelToken;
}

/// <summary>
/// Used for the do after event to free the entity that owns the <see cref="EnsnareableComponent"/>
/// </summary>
public sealed class FreeEnsnareDoAfterComplete : EntityEventArgs
{
    public readonly EnsnaringComponent? EnsnaringComponent;

    public FreeEnsnareDoAfterComplete(EnsnaringComponent ensnaringComponent)
    {
        EnsnaringComponent = ensnaringComponent;
    }
}

/// <summary>
/// Used for the do after event when it fails to free the entity that owns the <see cref="EnsnareableComponent"/>
/// </summary>
public sealed class FreeEnsnareDoAfterCancel : EntityEventArgs
{
    public readonly EnsnaringComponent? EnsnaringComponent;

    public FreeEnsnareDoAfterCancel(EnsnaringComponent ensnaringComponent)
    {
        EnsnaringComponent = ensnaringComponent;
    }
}
