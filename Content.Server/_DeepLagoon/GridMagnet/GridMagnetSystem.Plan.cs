using System.Numerics;
using Content.Shared._DeepLagoon.GridMagnet;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Content.Server.Shuttles.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._DeepLagoon.GridMagnet;

public sealed partial class GridMagnetSystem
{
    private enum PairPhase : byte
    {
        /// <summary>
        /// Both machines are active and lined up; waiting out the start delay.
        /// </summary>
        Charging,

        /// <summary>
        /// The pulled grid is moving toward its final place.
        /// </summary>
        Pulling,

        /// <summary>
        /// The grids touch; waiting out the weld time.
        /// </summary>
        Welding,
    }

    /// <summary>
    /// Everything fixed when two machines pair up: which grid stays, which one moves and is merged away,
    /// and how the pulled grid's tiles map onto the kept grid.
    /// </summary>
    private sealed class Plan
    {
        /// <summary>The machine on the kept grid.</summary>
        public EntityUid Keeper;
        /// <summary>The machine on the grid that is pulled and merged away.</summary>
        public EntityUid Pulled;
        public EntityUid KeeperGrid;
        public EntityUid PulledGrid;

        /// <summary>
        /// Quarter turns, counter-clockwise, applied to the pulled grid so the machines face each other.
        /// </summary>
        public int Turns;

        /// <summary>
        /// Origin of the pulled grid in kept grid tile coordinates, as <see cref="GridFixtureSystem.Merge"/> takes it.
        /// </summary>
        public Vector2i Offset;

        /// <summary>
        /// Tile of the kept grid the pulled machine ends up on: the one right in front of the kept machine.
        /// </summary>
        public Vector2i TargetTile;

        /// <summary>
        /// Centre of the pulled machine's tile in the pulled grid's own coordinates. The pull moves and turns
        /// the grid around this point, so the machine travels in a straight line.
        /// </summary>
        public Vector2 LocalPivot;
    }

    private sealed class Pair
    {
        public readonly Plan Plan;

        public Pair(Plan plan)
        {
            Plan = plan;
        }

        public EntityUid Keeper => Plan.Keeper;
        public EntityUid Pulled => Plan.Pulled;
        public EntityUid KeeperGrid => Plan.KeeperGrid;
        public EntityUid PulledGrid => Plan.PulledGrid;

        public PairPhase Phase;

        /// <summary>
        /// Charging: when the pull starts. Welding: when the merge happens.
        /// </summary>
        public TimeSpan PhaseEnd;

        public TimeSpan PullStart;
        public TimeSpan PullEnd;

        /// <summary>World position of the pulled machine when the pull started.</summary>
        public Vector2 Pivot0;

        /// <summary>World rotation of the pulled grid when the pull started.</summary>
        public Angle Angle0;

        /// <summary>World rotation of the kept grid when the pull started.</summary>
        public Angle KeeperAngle0;

        /// <summary>Radians the pulled grid turns over the whole pull, along the shorter way.</summary>
        public double AngleDelta;

        /// <summary>Where this system last put the pulled grid, to notice something else moving it.</summary>
        public Vector2 LastPos;
    }

    /// <summary>
    /// Unit tile step in front of a machine rotated by the given quarter turns (south at zero).
    /// </summary>
    private static Vector2i FrontOf(int turns)
    {
        return turns switch
        {
            0 => new Vector2i(0, -1),
            1 => new Vector2i(1, 0),
            2 => new Vector2i(0, 1),
            _ => new Vector2i(-1, 0),
        };
    }

    private static int QuarterTurns(Angle angle)
    {
        return ((int) Math.Round(angle.Theta / MathHelper.PiOver2) % 4 + 4) % 4;
    }

    /// <summary>
    /// Rotates a tile index counter-clockwise about the grid origin. Tiles turn around their lower-left corner,
    /// the same way <see cref="GridFixtureSystem.Merge"/> turns them, so this matches it exactly.
    /// </summary>
    private static Vector2i RotateTile(Vector2i tile, int turns)
    {
        return turns switch
        {
            0 => tile,
            1 => new Vector2i(-tile.Y - 1, tile.X),
            2 => new Vector2i(-tile.X - 1, -tile.Y - 1),
            _ => new Vector2i(tile.Y, -tile.X - 1),
        };
    }

    /// <summary>
    /// Same turn for a continuous point in grid coordinates, such as a decal position.
    /// </summary>
    private static Vector2 RotatePoint(Vector2 point, int turns)
    {
        return turns switch
        {
            0 => point,
            1 => new Vector2(-point.Y, point.X),
            2 => new Vector2(-point.X, -point.Y),
            _ => new Vector2(point.Y, -point.X),
        };
    }

    private int CountTiles(EntityUid grid, MapGridComponent comp)
    {
        var count = 0;
        var enumerator = _map.GetAllTilesEnumerator(grid, comp);
        while (enumerator.MoveNext(out _))
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Counts the tiles of the pulled grid that would land on an occupied tile of the kept grid.
    /// </summary>
    private int CountOverlap(Plan plan)
    {
        if (!TryComp(plan.KeeperGrid, out MapGridComponent? keeperGrid) ||
            !TryComp(plan.PulledGrid, out MapGridComponent? pulledGrid))
        {
            return int.MaxValue;
        }

        var count = 0;
        var enumerator = _map.GetAllTilesEnumerator(plan.PulledGrid, pulledGrid);
        while (enumerator.MoveNext(out var tile))
        {
            var destination = RotateTile(tile.Value.GridIndices, plan.Turns) + plan.Offset;
            if (_map.TryGetTile(keeperGrid, destination, out var existing) && !existing.IsEmpty)
                count++;
        }

        return count;
    }

    /// <summary>
    /// Decides which grid stays and which one is pulled, and works out how the two fit together.
    /// The grid of a station wins over one that is not; failing that, the grid with more tiles stays.
    /// </summary>
    private bool TryBuildPlan(
        Entity<GridMagnetComponent, TransformComponent> a,
        Entity<GridMagnetComponent, TransformComponent> b,
        out Plan? plan,
        out string? error)
    {
        plan = null;
        error = null;

        var gridA = a.Comp2.GridUid!.Value;
        var gridB = b.Comp2.GridUid!.Value;

        if (HasComp<FTLComponent>(gridA) || HasComp<FTLComponent>(gridB))
        {
            error = Loc.GetString("dl-grid-magnet-fail-ftl");
            return false;
        }

        if ((HasComp<ShuttleDeedComponent>(gridA) || HasComp<ShuttleDeedComponent>(gridB)) &&
            !(a.Comp1.AllowDeededGrids && b.Comp1.AllowDeededGrids))
        {
            error = Loc.GetString("dl-grid-magnet-fail-deed");
            return false;
        }

        var gridCompA = Comp<MapGridComponent>(gridA);
        var gridCompB = Comp<MapGridComponent>(gridB);

        var memberA = TryComp(gridA, out StationMemberComponent? stationA);
        var memberB = TryComp(gridB, out StationMemberComponent? stationB);

        if (memberA && memberB && stationA!.Station != stationB!.Station)
        {
            error = Loc.GetString("dl-grid-magnet-fail-stations");
            return false;
        }

        bool aKeeps;
        if (memberA != memberB)
        {
            aKeeps = memberA;
        }
        else
        {
            var tilesA = CountTiles(gridA, gridCompA);
            var tilesB = CountTiles(gridB, gridCompB);
            aKeeps = tilesA != tilesB ? tilesA > tilesB : gridA.Id < gridB.Id;
        }

        var keeper = aKeeps ? a : b;
        var pulled = aKeeps ? b : a;
        var keeperGrid = aKeeps ? gridA : gridB;
        var pulledGrid = aKeeps ? gridB : gridA;
        var keeperGridComp = aKeeps ? gridCompA : gridCompB;
        var pulledGridComp = aKeeps ? gridCompB : gridCompA;

        // The pulled grid is deleted by the merge, and a docking joint to it would go with it.
        var docks = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (docks.MoveNext(out _, out var dock, out var dockXform))
        {
            if (dockXform.GridUid == pulledGrid && dock.Docked)
            {
                error = Loc.GetString("dl-grid-magnet-fail-docked");
                return false;
            }
        }

        var tileKeeper = _map.CoordinatesToTile(keeperGrid, keeperGridComp, keeper.Comp2.Coordinates);
        var tilePulled = _map.CoordinatesToTile(pulledGrid, pulledGridComp, pulled.Comp2.Coordinates);

        // The pulled machine has to end up facing the kept one, one tile in front of it. Facing is the machine's
        // local rotation, which is relative to its grid, so all of this is exact quarter-turn integer math.
        var turnsKeeper = QuarterTurns(keeper.Comp2.LocalRotation);
        var turnsPulled = QuarterTurns(pulled.Comp2.LocalRotation);
        var turns = ((turnsKeeper + 2 - turnsPulled) % 4 + 4) % 4;
        var target = tileKeeper + FrontOf(turnsKeeper);

        plan = new Plan
        {
            Keeper = keeper,
            Pulled = pulled,
            KeeperGrid = keeperGrid,
            PulledGrid = pulledGrid,
            Turns = turns,
            TargetTile = target,
            Offset = target - RotateTile(tilePulled, turns),
            LocalPivot = _map.GridTileToLocal(pulledGrid, pulledGridComp, tilePulled).Position,
        };

        var overlap = CountOverlap(plan);
        if (overlap > 0)
        {
            error = Loc.GetString("dl-grid-magnet-fail-overlap", ("count", overlap));
            plan = null;
            return false;
        }

        if (PullSeconds(plan, keeper.Comp1) > keeper.Comp1.MaxPullTime)
        {
            error = Loc.GetString("dl-grid-magnet-fail-too-long");
            plan = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// World position and rotation the pulled machine and grid have to reach. Read from the kept grid every time,
    /// so a kept grid that drifts or turns while the pull runs is followed.
    /// </summary>
    private void GetTarget(Plan plan, out Vector2 pivot, out Angle angle)
    {
        var centre = _map.GridTileToLocal(plan.KeeperGrid, Comp<MapGridComponent>(plan.KeeperGrid), plan.TargetTile);
        pivot = _transform.ToMapCoordinates(centre).Position;
        angle = _transform.GetWorldRotation(plan.KeeperGrid) + plan.Turns * MathHelper.PiOver2;
    }

    /// <summary>
    /// How long the pull would take from the current positions with the given speeds.
    /// </summary>
    private double PullSeconds(Plan plan, GridMagnetComponent magnet)
    {
        GetTarget(plan, out var pivot1, out var angle1);

        var distance = (pivot1 - _transform.GetWorldPosition(plan.Pulled)).Length();
        var turn = Math.Abs(Angle.ShortestDistance(_transform.GetWorldRotation(plan.PulledGrid), angle1).Theta);

        var byMove = distance / Math.Max(magnet.PullSpeed, 0.01f);
        var byTurn = turn / Math.Max(MathHelper.DegreesToRadians(magnet.PullRotationSpeed), 0.001);

        return Math.Max(Math.Max(byMove, byTurn), magnet.MinPullTime);
    }

    private void BeginPull(Pair pair, GridMagnetComponent keeperMagnet, TimeSpan now)
    {
        var plan = pair.Plan;

        var seconds = PullSeconds(plan, keeperMagnet);
        if (seconds > keeperMagnet.MaxPullTime)
        {
            // The machines drifted apart (or were retuned) since pairing; this cannot finish in time.
            NotifyBoth(pair, "dl-grid-magnet-failed", ("reason", Loc.GetString("dl-grid-magnet-fail-too-long")));
            EndPair(pair, deactivate: true, null);
            return;
        }

        GetTarget(plan, out _, out var angle1);

        pair.Pivot0 = _transform.GetWorldPosition(plan.Pulled);
        pair.Angle0 = _transform.GetWorldRotation(plan.PulledGrid);
        pair.KeeperAngle0 = _transform.GetWorldRotation(plan.KeeperGrid);
        pair.AngleDelta = Angle.ShortestDistance(pair.Angle0, angle1).Theta;
        pair.LastPos = _transform.GetWorldPosition(plan.PulledGrid);
        pair.PullStart = now;
        pair.PullEnd = now + TimeSpan.FromSeconds(seconds);
        pair.Phase = PairPhase.Pulling;

        NotifyBoth(pair, "dl-grid-magnet-pulling");
    }

    /// <summary>
    /// Puts the pulled grid where it belongs at this moment of the pull. After the pull ends this keeps holding
    /// it at the final spot. Returns false when something else has moved the grid.
    /// </summary>
    private bool MovePulled(Pair pair, TimeSpan now)
    {
        var plan = pair.Plan;
        var grid = plan.PulledGrid;

        if ((_transform.GetWorldPosition(grid) - pair.LastPos).Length() > MaxDisturbance)
            return false;

        GetTarget(plan, out var pivot1, out _);

        var progress = 1f;
        if (pair.Phase == PairPhase.Pulling)
        {
            var total = (pair.PullEnd - pair.PullStart).TotalSeconds;
            progress = total <= 0 ? 1f : (float) Math.Clamp((now - pair.PullStart).TotalSeconds / total, 0, 1);
        }

        // Smoothstep, so the grid eases in and out instead of lurching.
        var eased = progress * progress * (3f - 2f * progress);

        var keeperDrift = Angle.ShortestDistance(pair.KeeperAngle0, _transform.GetWorldRotation(plan.KeeperGrid)).Theta;
        var angle = new Angle(pair.Angle0.Theta + (pair.AngleDelta + keeperDrift) * eased);
        var pivot = Vector2.Lerp(pair.Pivot0, pivot1, eased);

        var origin = pivot - angle.RotateVec(plan.LocalPivot);
        _transform.SetWorldPositionRotation(grid, origin, angle);

        // A moving shuttle would otherwise carry its old velocity on after the pull is dropped.
        _physics.SetLinearVelocity(grid, Vector2.Zero);
        _physics.SetAngularVelocity(grid, 0f);

        pair.LastPos = origin;
        return true;
    }
}
