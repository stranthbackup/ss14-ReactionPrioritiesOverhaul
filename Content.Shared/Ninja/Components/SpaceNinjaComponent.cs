using Content.Shared.Ninja.Systems;

namespace Content.Shared.Ninja.Components;

/// <summary>
/// Component placed on a mob to make it a space ninja, able to use suit and glove powers.
/// Contains ids of equipment
/// </summary>
// TODO: Contains objective related stuff, might want to move it out somehow
[Access(typeof(SharedNinjaSystem))]
[RegisterComponent]
public sealed class SpaceNinjaComponent : Component
{
    /// Currently worn suit
    [ViewVariables]
    public EntityUid? Suit = null;

    /// Currently worn gloves
    [ViewVariables]
    public EntityUid? Gloves = null;

    /// Bound katana, set once picked up and never removed
    [ViewVariables]
    public EntityUid? Katana = null;

    /// Number of doors that have been doorjacked, used for objective
    [ViewVariables]
    public int DoorsJacked = 0;

    /// Research nodes that have been downloaded, used for objective
    [ViewVariables]
    public HashSet<string> DownloadedNodes = new();

    /// Warp point that the spider charge has to target
    [ViewVariables]
    public EntityUid? SpiderChargeTarget = null;

    /// Whether the spider charge has been detonated on the target, used for objective
    [ViewVariables]
    public bool SpiderChargeDetonated;

    /// Whether the comms console has been hacked, used for objective
    [ViewVariables]
    public bool CalledInThreat;
}
