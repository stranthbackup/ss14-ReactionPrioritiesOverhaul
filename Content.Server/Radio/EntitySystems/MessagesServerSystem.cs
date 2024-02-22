using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Shared.Timing;
using Content.Shared.CartridgeLoader;
using Content.Server.CartridgeLoader;
using Content.Server.Radio.Components;
using Robust.Shared.Containers;
using Content.Server.Power.Components;

namespace Content.Server.Radio.EntitySystems;

public sealed class MessagesServerSystem : EntitySystem
{

    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private readonly MessagesCartridgeSystem _messagesCartridgeSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var serverQuery = EntityQueryEnumerator<MessagesServerComponent>();
        while (serverQuery.MoveNext(out var uid, out var server))
        {
            if (server.NextUpdate > _gameTiming.CurTime)
                continue;
            server.NextUpdate = _gameTiming.CurTime + server.UpdateDelay;

            Update(uid, server);
        }
    }

    public void Update(EntityUid uid, MessagesServerComponent component)
    {
        var mapId = Transform(uid).MapID;

        if (!TryComp(uid, out ApcPowerReceiverComponent? powerReceiver) || !(powerReceiver.Powered))
            return;

        var query = EntityQueryEnumerator<MessagesCartridgeComponent>();
        List<(string,string)> toUpdate = new();

        bool needDictUpdate = false;

        while(query.MoveNext(out var cartUid, out var cartComponent))
        {
            if (Transform(cartUid).MapID != mapId)
                continue;
            if ((cartComponent.UserUid == null) || (cartComponent.UserName == null))
                continue;
            if ((component.NameDict.ContainsKey(cartComponent.UserUid)) && (component.NameDict[cartComponent.UserUid] == cartComponent.UserName))
                continue;

            needDictUpdate = true;
            component.NameDict[cartComponent.UserUid] = cartComponent.UserName;
            toUpdate.Add((cartComponent.UserUid,cartComponent.UserName));
        }

        if (needDictUpdate)
        {
            query = EntityQueryEnumerator<MessagesCartridgeComponent>();
            while (query.MoveNext(out var cartUid,out var cartComponent))
            {
                foreach (var (key,value) in toUpdate)
                {
                    cartComponent.NameDict[key]=value;
                }
            }
        }

        if ((component.MessagesQueue.Count > 0))
        {
            var tempMessagesQueue = new List<MessagesMessageData>(component.MessagesQueue);
            foreach (var message in tempMessagesQueue)
            {
                bool sent=TryToSend(message, mapId, component);
                if (sent) component.MessagesQueue.Remove(message);
            }
        }
    }

    public bool TryToSend(MessagesMessageData message, MapId mapId, MessagesServerComponent server)
    {
        bool sent = false;

        var query = EntityQueryEnumerator<CartridgeLoaderComponent, ContainerManagerComponent>();

        while (query.MoveNext(out var uid, out var comp, out var cont))
        {
            if (!_cartridgeLoaderSystem.TryGetProgram<MessagesCartridgeComponent>(uid, out var progUid, out var messagesCartridgeComponent, false, comp, cont))
                continue;
            if (progUid is EntityUid realProgUid)
            {
                if (messagesCartridgeComponent.UserUid == message.ReceiverId)
                    _messagesCartridgeSystem.ServerToPdaMessage(realProgUid, messagesCartridgeComponent, message, uid, server);
                sent = true;
            }
        }

        return sent;
    }

    public void PdaToServerMessage(EntityUid uid, MessagesServerComponent component, MessagesMessageData message)
    {
        component.Messages.Add(message);
        component.MessagesQueue.Add(message);
        Update(uid, component);
    }

}
