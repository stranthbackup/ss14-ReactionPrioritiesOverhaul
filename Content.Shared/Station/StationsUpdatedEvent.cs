﻿using Robust.Shared.Serialization;

namespace Content.Shared.Station;

[NetSerializable, Serializable]
public sealed class StationsUpdatedEvent : EntityEventArgs
{
    public readonly HashSet<EntityUid> Stations;

    public StationsUpdatedEvent(HashSet<EntityUid> stations)
    {
        Stations = stations;
    }
}
