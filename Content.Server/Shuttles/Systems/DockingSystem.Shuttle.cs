using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class DockingSystem
{
    /*
     * Handles the shuttle side of FTL docking.
     */

    public Angle GetAngle(EntityUid uid, TransformComponent xform, EntityUid targetUid, TransformComponent targetXform, EntityQuery<TransformComponent> xformQuery)
   {
       var (shuttlePos, shuttleRot) = _transform.GetWorldPositionRotation(xform, xformQuery);
       var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetXform, xformQuery);

       var shuttleCOM = Robust.Shared.Physics.Transform.Mul(new Transform(shuttlePos, shuttleRot),
           Comp<PhysicsComponent>(uid).LocalCenter);
       var targetCOM = Robust.Shared.Physics.Transform.Mul(new Transform(targetPos, targetRot),
           Comp<PhysicsComponent>(targetUid).LocalCenter);

       var mapDiff = shuttleCOM - targetCOM;
       var angle = mapDiff.ToWorldAngle();
       angle -= targetRot;
       return angle;
   }

   /// <summary>
   /// Checks if 2 docks can be connected by moving the shuttle directly onto docks.
   /// </summary>
   private bool CanDock(
       DockingComponent shuttleDock,
       TransformComponent shuttleDockXform,
       DockingComponent gridDock,
       TransformComponent gridDockXform,
       Angle targetGridRotation,
       Box2 shuttleAABB,
       MapGridComponent grid,
       [NotNullWhen(true)] out Box2? shuttleDockedAABB,
       out Matrix3 matty,
       out Angle gridRotation)
   {
       gridRotation = Angle.Zero;
       matty = Matrix3.Identity;
       shuttleDockedAABB = null;

       if (shuttleDock.Docked ||
           gridDock.Docked ||
           !shuttleDockXform.Anchored ||
           !gridDockXform.Anchored)
       {
           return false;
       }

       // First, get the station dock's position relative to the shuttle, this is where we rotate it around
       var stationDockPos = shuttleDockXform.LocalPosition +
                            shuttleDockXform.LocalRotation.RotateVec(new Vector2(0f, -1f));

       // Need to invert the grid's angle.
       var shuttleDockAngle = shuttleDockXform.LocalRotation;
       var gridDockAngle = gridDockXform.LocalRotation.Opposite();

       var stationDockMatrix = Matrix3.CreateInverseTransform(stationDockPos, shuttleDockAngle);
       var gridXformMatrix = Matrix3.CreateTransform(gridDockXform.LocalPosition, gridDockAngle);
       Matrix3.Multiply(in stationDockMatrix, in gridXformMatrix, out matty);
       shuttleDockedAABB = matty.TransformBox(shuttleAABB);
       // Rounding moment
       shuttleDockedAABB = shuttleDockedAABB.Value.Enlarged(-0.01f);

       if (!ValidSpawn(grid, shuttleDockedAABB.Value))
           return false;

       gridRotation = targetGridRotation + gridDockAngle - shuttleDockAngle;
       return true;
   }

   public DockingConfig? GetDockingConfig(EntityUid shuttleUid, EntityUid targetGrid)
    {
       var gridDocks = GetDocks(targetGrid);

       if (gridDocks.Count <= 0)
           return null;

       var xformQuery = GetEntityQuery<TransformComponent>();
       var targetGridGrid = Comp<MapGridComponent>(targetGrid);
       var targetGridXform = xformQuery.GetComponent(targetGrid);
       var targetGridAngle = _transform.GetWorldRotation(targetGridXform).Reduced();

       var shuttleDocks = GetDocks(shuttleUid);
       var shuttleAABB = Comp<MapGridComponent>(shuttleUid).LocalAABB;

       var validDockConfigs = new List<DockingConfig>();

       if (shuttleDocks.Count > 0)
       {
           // We'll try all combinations of shuttle docks and see which one is most suitable
           foreach (var (dockUid, shuttleDock) in shuttleDocks)
           {
               var shuttleDockXform = xformQuery.GetComponent(dockUid);

               foreach (var (gridDockUid, gridDock) in gridDocks)
               {
                   var gridXform = xformQuery.GetComponent(gridDockUid);

                   if (!CanDock(
                           shuttleDock, shuttleDockXform,
                           gridDock, gridXform,
                           targetGridAngle,
                           shuttleAABB,
                           targetGridGrid,
                           out var dockedAABB,
                           out var matty,
                           out var targetAngle))
                   {
                       continue;
                   }

                   // Can't just use the AABB as we want to get bounds as tight as possible.
                   var spawnPosition = new EntityCoordinates(targetGrid, matty.Transform(Vector2.Zero));
                   spawnPosition = new EntityCoordinates(targetGridXform.MapUid!.Value, spawnPosition.ToMapPos(EntityManager, _transform));

                   var dockedBounds = new Box2Rotated(shuttleAABB.Translated(spawnPosition.Position), targetGridAngle, spawnPosition.Position);

                   // Check if there's no intersecting grids (AKA oh god it's docking at cargo).
                   if (_mapManager.FindGridsIntersecting(targetGridXform.MapID,
                           dockedBounds).Any(o => o.Owner != targetGrid))
                   {
                       continue;
                   }

                   // Alright well the spawn is valid now to check how many we can connect
                   // Get the matrix for each shuttle dock and test it against the grid docks to see
                   // if the connected position / direction matches.

                   var dockedPorts = new List<(EntityUid DockAUid, EntityUid DockBUid, DockingComponent DockA, DockingComponent DockB)>()
                   {
                       (dockUid, gridDockUid, shuttleDock, gridDock),
                   };

                   // TODO: Check shuttle orientation as the tiebreaker.

                   foreach (var (otherUid, other) in shuttleDocks)
                   {
                       if (other == shuttleDock)
                           continue;

                       foreach (var (otherGridUid, otherGrid) in gridDocks)
                       {
                           if (otherGrid == gridDock)
                               continue;

                           if (!CanDock(
                                   other,
                                   xformQuery.GetComponent(otherUid),
                                   otherGrid,
                                   xformQuery.GetComponent(otherGridUid),
                                   targetGridAngle,
                                   shuttleAABB, targetGridGrid,
                                   out var otherDockedAABB,
                                   out _,
                                   out var otherTargetAngle) ||
                               !otherDockedAABB.Equals(dockedAABB) ||
                               !targetAngle.Equals(otherTargetAngle))
                           {
                               continue;
                           }

                           dockedPorts.Add((otherUid, otherGridUid, other, otherGrid));
                       }
                   }

                   validDockConfigs.Add(new DockingConfig()
                   {
                       Docks = dockedPorts,
                       Area = dockedAABB.Value,
                       Coordinates = spawnPosition,
                       Angle = targetAngle,
                   });
               }
           }
       }

       if (validDockConfigs.Count <= 0)
           return null;

       // Prioritise by priority docks, then by maximum connected ports, then by most similar angle.
       validDockConfigs = validDockConfigs
           .OrderByDescending(x => x.Docks.Any(docks => HasComp<EmergencyDockComponent>(docks.DockB.Owner)))
           .ThenByDescending(x => x.Docks.Count)
           .ThenBy(x => Math.Abs(Angle.ShortestDistance(x.Angle.Reduced(), targetGridAngle).Theta)).ToList();

       var location = validDockConfigs.First();
       location.TargetGrid = targetGrid;
       // TODO: Ideally do a hyperspace warpin, just have it run on like a 10 second timer.

       return location;
    }

   /// <summary>
   /// Checks whether the emergency shuttle can warp to the specified position.
   /// </summary>
   private bool ValidSpawn(MapGridComponent grid, Box2 area)
   {
       return !grid.GetLocalTilesIntersecting(area).Any();
   }

   public List<(EntityUid Uid, DockingComponent Component)> GetDocks(EntityUid uid)
   {
       var result = new List<(EntityUid Uid, DockingComponent Component)>();
       var query = AllEntityQuery<DockingComponent, TransformComponent>();

       while (query.MoveNext(out var dockUid, out var dock, out var xform))
       {
           if (xform.ParentUid != uid || !dock.Enabled)
               continue;

           result.Add((dockUid, dock));
       }

       return result;
   }
}
