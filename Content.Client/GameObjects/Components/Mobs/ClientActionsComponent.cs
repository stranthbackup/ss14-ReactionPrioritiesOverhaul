﻿using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.GameObjects.EntitySystems;
using Content.Client.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Stylesheets;
using Content.Shared.Actions;
using Content.Shared.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Client.Interfaces.ResourceManagement;
using Robust.Client.Interfaces.UserInterface;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Client.GameObjects.Components.Mobs
{
    /// <inheritdoc/>
    [RegisterComponent]
    [ComponentReference(typeof(SharedActionsComponent))]
    public sealed class ClientActionsComponent : SharedActionsComponent
    {
        private static readonly float TooltipTextMaxWidth = 280;
        public static readonly byte Hotbars = 10;
        public static readonly byte Slots = 10;

        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private ActionsUI _ui;
        private ActionMenu _menu;
        private PanelContainer _tooltip;
        private RichTextLabel _actionName;
        private RichTextLabel _actionDescription;
        private RichTextLabel _actionCooldown;
        private RichTextLabel _actionRequirements;
        private bool _tooltipReady;
        // tracks the action slot we are currently selecting a target for
        private ActionSlot _selectingTargetFor;

        /// <summary>
        /// Allows calculating if we need to act due to this component being controlled by the current mob
        /// TODO: should be revisited after space-wizards/RobustToolbox#1255
        /// </summary>
        [ViewVariables]
        private bool CurrentlyControlled => _playerManager.LocalPlayer != null && _playerManager.LocalPlayer.ControlledEntity == Owner;


        // the slots and assignments fields hold client's assignments (what action goes in what slot),
        // which are completely client side and independent of what actions they've actually been granted.

        /// <summary>
        /// x = hotbar number, y = slot of that hotbar (index 0 corresponds to the one labeled "1",
        /// index 9 corresponds to the one labeled "0"). Essentially the inverse of _assignments.
        /// </summary>
        private ActionType?[,] _slots = new ActionType?[Hotbars, Slots];

        /// <summary>
        /// Hotbar and slot assignment for each action type (slot index 0 corresponds to the one labeled "1",
        /// slot index 9 corresponds to the one labeled "0"). The key corresponds to an index in the _slots array.
        /// The value is a list because actions can be assigned to multiple slots. Even if an action type has not been granted,
        /// it can still be assigned to a slot. Essentially the inverse of _slots.
        /// There will be no entry if there is no assignment (no empty lists in this dict)
        /// </summary>
        private Dictionary<ActionType, List<(byte Hotbar, byte Slot)>> _assignments = new Dictionary<ActionType, List<(byte Hotbar, byte Slot)>>();

        /// <summary>
        /// Actions which have been manually cleared by the user, thus should not
        /// auto-populate.
        /// </summary>
        private HashSet<ActionType> _manuallyClearedActions = new HashSet<ActionType>();

        // index of currently displayed hotbar
        private byte _selectedHotbar = 0;

        protected override void Shutdown()
        {
            base.Shutdown();
            PlayerDetached();
        }

        public override void HandleMessage(ComponentMessage message, IComponent component)
        {
            base.HandleMessage(message, component);
            switch (message)
            {
                case PlayerAttachedMsg _:
                    PlayerAttached();
                    break;
                case PlayerDetachedMsg _:
                    PlayerDetached();
                    break;
            }
        }

        public override void HandleComponentState(ComponentState curState, ComponentState nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (!(curState is ActionComponentState))
            {
                return;
            }

            UpdateUI();
        }

        private void PlayerAttached()
        {
            if (!CurrentlyControlled || _ui != null)
            {
                return;
            }

            _ui = new ActionsUI(ActionOnOnShowTooltip, ActionOnOnHideTooltip, ActionSlotEventHandler,
                ActionDragDropHandler,
                NextHotbar,
                PreviousHotbar, OpenActionMenu);
            _menu = new ActionMenu(ActionOnOnShowTooltip, ActionOnOnHideTooltip, this, ActionMenuItemSelected);
            LayoutContainer.SetGrowHorizontal(_ui, LayoutContainer.GrowDirection.End);
            LayoutContainer.SetAnchorAndMarginPreset(_ui, LayoutContainer.LayoutPreset.TopLeft, margin: 10);
            LayoutContainer.SetMarginTop(_ui, 100);

            var uiManager = IoCManager.Resolve<IUserInterfaceManager>();
            uiManager.StateRoot.AddChild(_ui);

            _tooltip = new PanelContainer
            {
                Visible = false,
                StyleClasses = { StyleNano.StyleClassTooltipPanel }
            };
            var tooltipVBox = new VBoxContainer
            {
                RectClipContent = true
            };
            _tooltip.AddChild(tooltipVBox);
            _actionName = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleNano.StyleClassTooltipActionTitle }
            };
            tooltipVBox.AddChild(_actionName);
            _actionDescription = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleNano.StyleClassTooltipActionDescription }
            };
            tooltipVBox.AddChild(_actionDescription);
            _actionCooldown = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleNano.StyleClassTooltipActionCooldown }
            };
            tooltipVBox.AddChild(_actionCooldown);
            _actionRequirements = new RichTextLabel
            {
                MaxWidth = TooltipTextMaxWidth,
                StyleClasses = { StyleNano.StyleClassTooltipActionRequirements }
            };
            tooltipVBox.AddChild(_actionRequirements);

            uiManager.PopupRoot.AddChild(_tooltip);

            UpdateUI();
        }


        private void PlayerDetached()
        {
            StopTargeting();
            _menu?.Dispose();
            _ui?.Dispose();
            _ui = null;
        }

        /// <summary>
        /// Updates the displayed hotbar (and menu) based on current state of actions.
        /// </summary>
        private void UpdateUI()
        {
            if (!CurrentlyControlled || _ui == null)
            {
                return;
            }

            _menu?.UpdateActionStates();

            // if we've been granted any actions which have no assignment to any hotbar, we must auto-populate them
            // into the hotbar so the user knows about them.
            // We fill their current hotbar first, rolling over to the next open slot on the next hotbar.
            foreach (var actionState in EnumerateActionStates())
            {
                if (!_assignments.ContainsKey(actionState.ActionType))
                {
                    // don't auto populate stuff which the user has manually cleared
                    if (_manuallyClearedActions.Contains(actionState.ActionType)) continue;
                    AutoPopulate(actionState.ActionType);
                }
            }

            // now update the controls of only the current selected hotbar.
            for (byte i = 0; i < Slots; i++)
            {
                var actionType = _slots[_selectedHotbar, i];
                if (actionType == null)
                {
                    _ui.ClearSlot(i);
                    continue;
                }
                if (ActionManager.TryGet((ActionType) actionType, out var action))
                {
                    _ui.AssignSlot(i, action);
                }
                else
                {
                    Logger.WarningS("action", "unrecognized actionType {0}", actionType);
                    continue;
                }

                if (!IsGranted((ActionType) actionType))
                {
                    // just revoked an action we were trying to target with, stop targeting
                    if (_selectingTargetFor?.Action != null && _selectingTargetFor.Action.ActionType == actionType)
                    {
                        StopTargeting();
                    }
                    _ui.RevokeSlot(i);
                    continue;
                }
                _ui.GrantSlot(i);

                // check if we need to toggle it
                if (IsToggleable((ActionType) actionType, out var toggledOn))
                {
                    _ui.ToggleSlot(i, toggledOn);
                }
            }
        }

        private void NextHotbar(BaseButton.ButtonEventArgs args)
        {
            ChangeHotbar((byte) ((_selectedHotbar + 1) % Hotbars));
        }

        private void PreviousHotbar(BaseButton.ButtonEventArgs args)
        {
            int newBar = _selectedHotbar == 0 ? Hotbars - 1 : _selectedHotbar - 1;
            ChangeHotbar((byte) newBar);
        }

        private void ChangeHotbar(byte hotbar)
        {
            StopTargeting();
            _selectedHotbar = hotbar;
            _ui.SetHotbarLabel(hotbar + 1);

            UpdateUI();
        }

        private void OpenActionMenu(BaseButton.ButtonEventArgs args)
        {
            if (_menu.IsOpen)
            {
                _menu.Close();
            }
            else
            {
                _menu.Open();
            }
        }

        private void ActionMenuItemSelected(ActionMenuItemSelectedEventArgs args)
        {
            AutoPopulate(args.Action.ActionType);
            UpdateUI();
        }


        /// <summary>
        /// Finds the next open slot the action can go in and assigns it there,
        /// starting from the currently selected hotbar.
        /// Does not update any UI elements, only updates the assignment data structures.
        /// </summary>
        private void AutoPopulate(ActionType actionType)
        {
            for (byte hotbarOffset = 0; hotbarOffset < Hotbars; hotbarOffset++)
            {
                for (byte slot = 0; slot < Slots; slot++)
                {
                    if (_slots[(_selectedHotbar + hotbarOffset) % Hotbars, slot] != null) continue;
                    AssignSlot(_selectedHotbar, slot, actionType);
                    return;
                }
            }
            // there was no empty slot
        }

        /// <summary>
        /// Assigns the indicated hotbar slot to the specified action type.
        /// </summary>
        /// <param name="hotbar">hotbar whose slot is being assigned</param>
        /// <param name="slot">slot of the hotbar to assign to (0 = the slot labeled 1, 9 = the slot labeled 0)</param>
        /// <param name="actionType">action to assign to the slot</param>
        private void AssignSlot(byte hotbar, byte slot, ActionType actionType)
        {
            _slots[hotbar, slot] = actionType;
            if (_assignments.TryGetValue(actionType, out var slotList))
            {
                slotList.Add((hotbar, slot));
            }
            else
            {
                var newList = new List<(byte Hotbar, byte Slot)> {(hotbar, slot)};
                _assignments[actionType] = newList;
            }
        }

        /// <summary>
        /// Clear the assignment to the indicated slot.
        /// </summary>
        /// <param name="hotbar">hotbar whose slot is being cleared</param>
        /// <param name="slot">slot of the hotbar to clear (0 = the slot labeled 1, 9 = the slot labeled 0)</param>
        private void ClearSlot(byte hotbar, byte slot)
        {
            // remove this particular assignment from our data structures
            // (keeping in mind something can be assigned multiple slots)
            var currentAction = _slots[hotbar, slot];
            if (currentAction == null) return;
            var assignmentList = _assignments[(ActionType) currentAction];
            assignmentList = assignmentList.Where(a => a.Hotbar != _selectedHotbar || a.Slot != slot).ToList();
            if (assignmentList.Count == 0)
            {
                _assignments.Remove((ActionType) currentAction);
            }
            else
            {
                _assignments[(ActionType) currentAction] = assignmentList;
            }
            _slots[_selectedHotbar, slot] = null;
        }

        private void ActionSlotEventHandler(ActionSlotEventArgs args)
        {
            if (_ui.IsDragging) return;
            if (args.ActionSlotEvent == ActionSlotEvent.RightClick)
            {
                // right click to clear the action
                _manuallyClearedActions.Add(args.Action.ActionType);

                ClearSlot(_selectedHotbar, args.ActionSlot.SlotIndex);

                StopTargeting();
                args.ActionSlot.Clear();
                return;
            }
            switch (args.Action.BehaviorType)
            {
                case BehaviorType.Instant:
                    // for instant actions, we immediately tell the server we're doing it
                    SendNetworkMessage(new PerformInstantActionMessage(args.Action.ActionType));
                    break;
                case BehaviorType.Toggle:
                    // for toggle actions, we immediately tell the server we're toggling it.
                    // Predictively toggle it on as well
                    ToggleAction(args.Action.ActionType, args.ToggleOn);
                    SendNetworkMessage(new PerformToggleActionMessage(args.Action.ActionType, args.ToggleOn));
                    break;
                case BehaviorType.TargetPoint:
                case BehaviorType.TargetEntity:
                    // for target actions, we go into "select target" mode, we don't
                    // message the server until we actually pick our target.

                    // if we're clicking the same thing we're already targeting for, then we simply cancel
                    // targeting
                    if (_selectingTargetFor == args.ActionSlot)
                    {
                        StopTargeting();
                        break;
                    }
                    StartTargeting(args.ActionSlot);
                    break;
                default:
                    Logger.WarningS("action", "unhandled action press for action {0}", args.Action.ActionType);
                    break;
            }
        }

        private void ActionDragDropHandler(ActionSlotDragDropEventArgs obj)
        {
            // swap the 2 slots
            var fromAction = obj.FromSlot.Action;
            var fromIdx = obj.FromSlot.SlotIndex;
            var toAction = obj.ToSlot.Action;
            var toIdx = obj.ToSlot.SlotIndex;

            if (fromIdx == toIdx) return;

            AssignSlot(_selectedHotbar, toIdx, fromAction.ActionType);
            if (toAction != null)
            {
                AssignSlot(_selectedHotbar, fromIdx, toAction.ActionType);
            }
            else
            {
                ClearSlot(_selectedHotbar, fromIdx);
            }

            UpdateUI();
        }

        /// <summary>
        /// Puts us in targeting mode, where we need to pick either a target point or entity
        /// </summary>
        private void StartTargeting(ActionSlot actionSlot)
        {
            // If we were targeting something else we should stop
            StopTargeting();

            _selectingTargetFor = actionSlot;

            // show it as pressed to indicate we are currently selecting a target for it
            if (!actionSlot.Pressed)
            {
                actionSlot.Pressed = true;
            }

            // intercept clicks in the game world, treating them as our target selection. We want to
            // take priority before any other systems handle the click.
            CommandBinds.Builder
                .BindBefore(EngineKeyFunctions.Use, new PointerInputCmdHandler(TargetingOnUse),
                    typeof(ConstructionSystem), typeof(DragDropSystem))
                .Register<ClientActionsComponent>();
        }

        private bool TargetingOnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
        {
            // not currently predicted
            if (EntitySystem.Get<InputSystem>().Predicted) return false;

            // only do something for actual target-based actions
            if (_selectingTargetFor?.Action == null ||
                (_selectingTargetFor.Action.BehaviorType != BehaviorType.TargetEntity &&
                _selectingTargetFor.Action.BehaviorType != BehaviorType.TargetPoint)) return false;

            if (_selectingTargetFor.Action.BehaviorType == BehaviorType.TargetPoint)
            {
                // send our action to the server, we chose our target
                SendNetworkMessage(new PerformTargetPointActionMessage(_selectingTargetFor.Action.ActionType,
                    args.Coordinates));
                StopTargeting();
                return true;
            }
            if (_selectingTargetFor.Action.BehaviorType == BehaviorType.TargetEntity)
            {
                // target the currently hovered entity, if there is one
                if (args.EntityUid != EntityUid.Invalid)
                {
                    // send our action to the server, we chose our target
                    SendNetworkMessage(new PerformTargetEntityActionMessage(_selectingTargetFor.Action.ActionType,
                        args.EntityUid));
                    StopTargeting();
                    return true;
                }
            }

            StopTargeting();
            return false;
        }

        /// <summary>
        /// Switch out of targeting mode if currently selecting target for an action
        /// </summary>
        private void StopTargeting()
        {
            CommandBinds.Unregister<ClientActionsComponent>();
            if (_selectingTargetFor != null)
            {
                if (_selectingTargetFor.Pressed)
                {
                    _selectingTargetFor.Pressed = false;
                }
                _selectingTargetFor = null;
            }
        }

        private void ActionOnOnHideTooltip(object sender, EventArgs e)
        {
            _tooltipReady = false;
            _tooltip.Visible = false;
        }

        private void ActionOnOnShowTooltip(object sender, EventArgs e)
        {
            // this can come from an ActionSlot or an ActionMenuItem depending on if its for the
            // action hotbar or the action menu

            ActionPrototype action = null;
            int? totalCooldownDuration = null;
            if (sender is ActionSlot actionSlot)
            {
                action = actionSlot.Action;
                totalCooldownDuration = actionSlot.TotalDuration;
            }
            else if (sender is ActionMenuItem actionMenuItem)
            {
                action = actionMenuItem.Action;
            }
            else
            {
                // coding error, we got an unexpected sender
                throw new InvalidOperationException();
            }

            if (action == null) return;

            _actionName.SetMessage(action.Name);
            _actionDescription.SetMessage(action.Description);
            // check for a cooldown
            if (totalCooldownDuration != null && totalCooldownDuration > 0)
            {
                _actionCooldown.SetMessage(FormattedMessage.FromMarkup("[color=#776a6a]" +
                                                                       totalCooldownDuration +
                                                                      " sec cooldown[/color]"));
                _actionCooldown.Visible = true;
            }
            else
            {
                _actionCooldown.Visible = false;
            }
            //check for requirements message
            if (action.Requires != null)
            {
                _actionCooldown.SetMessage(FormattedMessage.FromMarkup("[color=#635c5c]" +
                                                                       action.Requires +
                                                                       "[/color]"));
            }
            else
            {
                _actionRequirements.Visible = false;
            }


            Tooltips.PositionTooltip(_tooltip);
            // if we set it visible here the size of the previous tooltip will flicker for a frame,
            // so instead we wait until FrameUpdate to make it visible
            _tooltipReady = true;
        }

        public void FrameUpdate(float frameTime)
        {
            if (_tooltipReady)
            {
                _tooltipReady = false;
                _tooltip.Visible = true;
            }
            // update the cooldowns for each currently displayed hotbar slot.
            // note that we don't actually need to keep track of cooldowns for
            // slots in other hotbars - since we store the precise start and end of each
            // cooldown we have no need to actively tick down, we can always calculate current
            // cooldown amount as-needed (for example when switching toolbars).
            for (byte i = 0; i < Slots; i++)
            {
                var actionType = _slots[_selectedHotbar, i];
                if (actionType == null) continue;

                if (!TryGetGrantedActionState((ActionType) actionType, out var actionState)) continue;
                _ui.UpdateCooldown(i, actionState.Cooldown, _gameTiming.CurTime);
            }
        }

        protected override void AfterGrantAction()
        {
            UpdateUI();
        }

        protected override void AfterRevokeAction()
        {
            UpdateUI();
        }

        protected override void AfterToggleAction()
        {
            UpdateUI();
        }
    }
}
