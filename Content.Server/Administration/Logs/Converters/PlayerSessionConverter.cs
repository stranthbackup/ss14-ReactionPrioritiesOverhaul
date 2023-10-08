﻿using System.Text.Json;

namespace Content.Server.Administration.Logs.Converters;

[AdminLogConverter]
public sealed class PlayerSessionConverter : AdminLogConverter<SerializablePlayer>
{
    public override void Write(Utf8JsonWriter writer, SerializablePlayer value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.Uid is {Valid: true} playerEntity)
        {
            writer.WriteNumber("id", playerEntity.Id);
            writer.WriteString("name", value.Name);
        }

        writer.WriteString("player", value.User);

        writer.WriteEndObject();
    }
}

public readonly record struct SerializablePlayer(EntityUid? Uid, string? Name, Guid User);
