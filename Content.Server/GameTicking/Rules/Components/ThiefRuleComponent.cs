using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Player;
using Content.Shared.Preferences;

namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// Stores data for <see cref="ThiefRuleSystem/">.
/// </summary>
[RegisterComponent, Access(typeof(ThiefRuleSystem))]
public sealed partial class ThiefRuleComponent : Component
{
    /// <summary>
    /// A chance for this mode to be added to the game.
    /// </summary>
    [DataField]
    public float RuleChance = 1f;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<AntagPrototype>))]
    public string ThiefPrototypeId = "Thief";

    public Dictionary<ICommonSession, HumanoidCharacterProfile> StartCandidates = new();

    [DataField]
    public float MaxObjectiveDifficulty = 2.5f;

    [DataField]
    public int MaxStealObjectives = 10;

    /// <summary>
    /// Things that will be given to thieves
    /// </summary>
    [DataField]
    public List<EntProtoId> StarterItems = new List<EntProtoId> { "ToolboxThief", "ThievingGloves" }; //TO DO - replace to chameleon thieving gloves whem merg

    /// <summary>
    /// All Thiefes created by this rule
    /// </summary>
    public readonly List<EntityUid> ThiefMinds = new();

    /// <summary>
    /// Max Thiefs created by rule on roundstart
    /// </summary>
    [DataField]
    public int MaxAllowThief = 3;

    /// <summary>
    /// Sound played when making the player a thief via antag control or ghost role
    /// </summary>
    [DataField]
    public SoundSpecifier? GreetingSound = new SoundPathSpecifier("/Audio/Misc/thief_greeting.ogg");
}
