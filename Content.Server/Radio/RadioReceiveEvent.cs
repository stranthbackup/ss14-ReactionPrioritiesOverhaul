using Content.Shared.Chat;
using Content.Shared.Radio;

namespace Content.Server.Radio;

public sealed class RadioReceiveEvent : EntityEventArgs
{
    public readonly string Message;
    public readonly EntityUid Source;
    public readonly RadioChannelPrototype Channel;
    public readonly MsgChatMessage ChatMsg;
    public readonly EntityUid? RadioSource;

    public RadioReceiveEvent(string message, EntityUid source, RadioChannelPrototype channel, MsgChatMessage chatMsg, EntityUid? radioSource)
    {
        Message = message;
        Source = source;
        Channel = channel;
        ChatMsg = chatMsg;
        RadioSource = radioSource;
    }
}

[ByRefEvent]
public record struct RadioReceiveAttemptEvent
{
    public readonly string Message;
    public readonly EntityUid Source;
    public readonly RadioChannelPrototype Channel;
    public readonly EntityUid? RadioSource;
    public bool Cancelled = false;

    public RadioReceiveAttemptEvent(string message, EntityUid source, RadioChannelPrototype channel, EntityUid? radioSource)
    {
        Message = message;
        Source = source;
        Channel = channel;
        RadioSource = radioSource;
    }
}
