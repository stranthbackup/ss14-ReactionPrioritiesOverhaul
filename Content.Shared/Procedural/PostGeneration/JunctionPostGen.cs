namespace Content.Shared.Procedural.PostGeneration;

/// <summary>
/// Places the specified entities at junction areas.
/// </summary>
public sealed partial class JunctionPostGen : IDunGenLayer
{
    /// <summary>
    /// Width to check for junctions.
    /// </summary>
    [DataField]
    public int Width = 3;
}
