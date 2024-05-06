using Robust.Shared.Audio;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(BloodBrotherRuleSystem))]
public sealed partial class BloodBrotherRuleComponent : Component
{
    public readonly List<EntityUid> Minds = new();
    public static readonly List<EntityUid> CommonObjectives = new();

    /// <summary>
    /// The total number of active blood brothers.
    /// </summary>
    public int NumberOfAntags => Minds.Count;

    /// <summary>
    /// Minimal amount of bros created.
    /// </summary>
    [DataField]
    public int MinBros = 2;

    /// <summary>
    /// Max amount of bros created.
    /// </summary>
    [DataField]
    public int MaxBros = 3;

    /// <summary>
    /// Max amount of objectives possible.
    /// </summary>
    [DataField]
    public int MaxObjectives = 5;

    /// <summary>
    /// Path to the traitor greeting sound.
    /// </summary>
    [DataField]
    public SoundSpecifier GreetingSound = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");
}
