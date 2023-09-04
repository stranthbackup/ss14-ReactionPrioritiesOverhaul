﻿using Content.Shared.CharacterInfo;
using Content.Shared.Objectives;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.CharacterInfo;

[InjectDependencies]
public sealed partial class CharacterInfoSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;

    public event Action<CharacterData>? OnCharacterUpdate;
    public event Action? OnCharacterDetached;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachSysMessage>(OnPlayerAttached);

        SubscribeNetworkEvent<CharacterInfoEvent>(OnCharacterInfoEvent);
    }

    public void RequestCharacterInfo()
    {
        var entity = _players.LocalPlayer?.ControlledEntity;
        if (entity == null)
        {
            return;
        }

        RaiseNetworkEvent(new RequestCharacterInfoEvent(entity.Value));
    }

    private void OnPlayerAttached(PlayerAttachSysMessage msg)
    {
        if (msg.AttachedEntity == default)
        {
            OnCharacterDetached?.Invoke();
        }
    }

    private void OnCharacterInfoEvent(CharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        var data = new CharacterData(msg.EntityUid, msg.JobTitle, msg.Objectives, msg.Briefing, Name(msg.EntityUid));
        OnCharacterUpdate?.Invoke(data);
    }

    public List<Control> GetCharacterInfoControls(EntityUid uid)
    {
        var ev = new GetCharacterInfoControlsEvent(uid);
        RaiseLocalEvent(uid, ref ev, true);
        return ev.Controls;
    }

    public readonly record struct CharacterData(
        EntityUid Entity,
        string Job,
        Dictionary<string, List<ConditionInfo>> Objectives,
        string Briefing,
        string EntityName
    );

    /// <summary>
    /// Event raised to get additional controls to display in the character info menu.
    /// </summary>
    [ByRefEvent]
    public readonly record struct GetCharacterInfoControlsEvent(EntityUid Entity)
    {
        public readonly List<Control> Controls = new();

        public readonly EntityUid Entity = Entity;
    }
}
