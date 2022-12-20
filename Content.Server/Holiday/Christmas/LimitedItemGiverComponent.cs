﻿using Content.Shared.Storage;
using Robust.Shared.Network;

namespace Content.Server.Holiday.Christmas;

/// <summary>
/// This is used for granting items to lucky souls, exactly once.
/// </summary>
[RegisterComponent, Access(typeof(LimitedItemGiverSystem))]
public sealed class LimitedItemGiverComponent : Component
{
    /// <summary>
    /// Santa knows who you are behind the screen, only one gift per player per round!
    /// </summary>
    public readonly HashSet<NetUserId> GrantedPlayers = new();

    [DataField("spawnEntries", required: true)]
    public List<EntitySpawnEntry> SpawnEntries = default!;

    [DataField("receivedPopup", required: true)]
    public string ReceivedPopup = default!;

    [DataField("deniedPopup", required: true)]
    public string DeniedPopup = default!;
}
