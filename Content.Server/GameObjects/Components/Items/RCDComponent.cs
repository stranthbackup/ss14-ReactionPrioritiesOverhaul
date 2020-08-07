﻿using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.BodySystem;
using Content.Server.GameObjects.Components.Fluids;
using Content.Server.GameObjects.EntitySystems.Click;
using Content.Server.Interfaces;
using Content.Server.Utility;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Items
{
    [RegisterComponent]
    public class RCDComponent : Component, IAfterInteract, IUse, IExamine
    {

#pragma warning disable 649
        [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager;
        [Dependency] private readonly IEntitySystemManager _entitySystemManager;
        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IServerEntityManager _serverEntityManager;
        [Dependency] private IServerNotifyManager _serverNotifyManager;
#pragma warning restore 649
        public override string Name => "RCD";
        private RcdMode _mode = 0; //What mode are we on? Can be floors, walls, deconstruct.
        private readonly RcdMode[] _modes = (RcdMode[])  Enum.GetValues(typeof(RcdMode));
        [ViewVariables(VVAccess.ReadWrite)] public int maxAmmo;
        public int _ammo; //How much "ammo" we have left. You can refill this with RCD ammo.


        ///Enum to store the different mode states for clarity.
        private enum RcdMode
        {
            Floors,
            Walls,
            Airlock,
            Deconstruct
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref maxAmmo, "maxAmmo", 5);
        }

        public override void Initialize()
        {
            base.Initialize();
            _ammo = maxAmmo;
        }

        ///<summary>
        /// Method called when the RCD is clicked in-hand, this will swap the RCD's mode from "floors" to "walls".
        ///</summary>

        public bool UseEntity(UseEntityEventArgs eventArgs)
        {
            SwapMode(eventArgs);
            return true;
        }

        ///<summary>
        ///Method to allow the user to swap the mode of the RCD by clicking it in hand, the actual in hand clicking bit is done over on UseEntity()
        ///@param UseEntityEventArgs = The entity which triggered this method call, used to know where to play the "click" sound.
        ///</summary>

        public void SwapMode(UseEntityEventArgs eventArgs)
        {
            _entitySystemManager.GetEntitySystem<AudioSystem>().PlayFromEntity("/Audio/Items/genhit.ogg", Owner);
            int mode = (int) _mode; //Firstly, cast our RCDmode mode to an int (enums are backed by ints anyway by default)
            mode = (++mode) % _modes.Length; //Then, do a rollover on the value so it doesnt hit an invalid state
            _mode = (RcdMode) mode; //Finally, cast the newly acquired int mode to an RCDmode so we can use it.
            _serverNotifyManager.PopupMessage(Owner, eventArgs.User, $"The RCD is now set to {this._mode} mode."); //Prints an overhead message above the RCD
        }

        ///<summary>
        ///Method called when the user examines this object, it'll simply add the mode that it's in to the object's description
        ///@params message = The original message from examining, like ..() in BYOND's examine
        ///</summary>

        public void Examine(FormattedMessage message, bool inDetailsRange)
        {
            message.AddMarkup(Loc.GetString("It's currently on {0} mode, and holds {1} charges.",_mode.ToString(), this._ammo));
        }

        ///<summary>
        /// Method to handle clicking on a tile to then appropriately RCD it. This can have several behaviours depending on mode.
        /// @param eventAargs = An action event telling us what tile was clicked on. We use this to exrapolate where to place the new tile / remove the old one etc.
        ///</summary>

        public void AfterInteract(AfterInteractEventArgs   eventArgs) // TODO: do_after()
        {
            var mapGrid = _mapManager.GetGrid(eventArgs.ClickLocation.GridID);
            var tile = mapGrid.GetTileRef(eventArgs.ClickLocation);
            var coordinates = mapGrid.GridTileToLocal(tile.GridIndices);
            //Less expensive checks first. Failing those ones, we need to check that the tile isn't obstructed.
            if (_ammo <= 0)
            {
                _serverNotifyManager.PopupMessage(Owner, eventArgs.User, $"The RCD is out of ammo!");
                return;
            }
            if (!InteractionChecks.InRangeUnobstructed(eventArgs))
            {
                return;
            }
            if (coordinates == GridCoordinates.InvalidGrid)
            {
                return;
            }

            var snapPos = mapGrid.SnapGridCellFor(eventArgs.ClickLocation, SnapGridOffset.Center);

            switch (_mode)
            {
                //Floor mode just needs the tile to be a space tile (subFloor)
                case RcdMode.Floors:
                    if (!tile.Tile.IsEmpty)
                    {
                        _serverNotifyManager.PopupMessage(Owner, eventArgs.User, $"You can only build a floor on space!");
                        return;
                    }

                    mapGrid.SetTile(eventArgs.ClickLocation, new Tile(_tileDefinitionManager["floor_steel"].TileId));
                    break;
                //We don't want to place a space tile on something that's already a space tile. Let's do the inverse of the last check.
                case RcdMode.Deconstruct:
                    if (tile.Tile.IsEmpty)
                    {
                        return;
                    }

                    if (!tile.IsBlockedTurf(true)) //Delete the turf
                    {
                        mapGrid.SetTile(snapPos, Tile.Empty);
                    }
                    else //Delete what the user targeted
                    {
                        //Don't delete mobs or puddles
                        if (eventArgs.Target == null || eventArgs.Target.TryGetComponent(out PuddleComponent puddleComponent) || eventArgs.Target.TryGetComponent<BodyManagerComponent>(out var bodyManagerComponent))
                        {
                            return;
                        }

                        eventArgs.Target.Delete();
                    }
                    break;
                //Walls are a special behaviour, and require us to build a new object with a transform rather than setting a grid tile, thus we early return to avoid the tile set code.
                case RcdMode.Walls:
                    if (tile.Tile.IsEmpty)
                    {
                        _serverNotifyManager.PopupMessage(Owner, eventArgs.User, $"Cannot build a wall on space!");
                        return;
                    }

                    if (tile.IsBlockedTurf(true))
                    {
                        _serverNotifyManager.PopupMessage(Owner, eventArgs.User, $"That tile is obstructed!");
                        return;
                    }
                    var ent = _serverEntityManager.SpawnEntity("solid_wall", mapGrid.GridTileToLocal(snapPos));
                    ent.Transform.LocalRotation = Owner.Transform.LocalRotation; //Now apply icon smoothing.
                    break;
                case RcdMode.Airlock:
                    if (tile.Tile.IsEmpty)
                    {
                        _serverNotifyManager.PopupMessage(Owner, eventArgs.User, $"Cannot build an airlock on space!");
                        return;
                    }
                    if (tile.IsBlockedTurf(true))
                    {
                        _serverNotifyManager.PopupMessage(Owner, eventArgs.User, $"That tile is obstructed!");
                        return;
                    }
                    var airlock = _serverEntityManager.SpawnEntity("Airlock", mapGrid.GridTileToLocal(snapPos));
                    airlock.Transform.LocalRotation = Owner.Transform.LocalRotation; //Now apply icon smoothing.
                    break;
                default:
                    return; //I don't know why this would happen, but sure I guess. Get out of here invalid state!
            }

            _entitySystemManager.GetEntitySystem<AudioSystem>().PlayFromEntity("/Audio/Items/deconstruct.ogg", Owner);
            _ammo--;

        }
    }
}
