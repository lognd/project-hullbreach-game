using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Ship;
using Hullbreach.Structure;

namespace Hullbreach.Bots
{
    // One block of a bot design in the order a player would place it.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#botship
    public readonly struct BotPlacement
    {
        public readonly int X;
        public readonly int Y;
        public readonly byte TypeId;
        public readonly byte Modifiers;
        public readonly bool Welded;

        public BotPlacement(int x, int y, byte typeId, byte modifiers, bool welded)
        {
            X = x; Y = y; TypeId = typeId; Modifiers = modifiers; Welded = welded;
        }
    }

    // A generated design: the finished grid, the placement order that builds it, and what it is made of.
    // frob:doc docs/reference/hullbreach-bots.md#botship
    public sealed class BotShip
    {
        public readonly BlockGrid Grid;
        public readonly IReadOnlyList<BotPlacement> Order;
        public readonly int Thrusters, Cannons, Fins, Retros, Armor;

        public BotShip(BlockGrid grid, List<BotPlacement> order)
        {
            Grid = grid;
            Order = order;
            foreach (var kv in grid.All)
            {
                switch (kv.Value.TypeId)
                {
                    case BlockTypes.Thruster: Thrusters++; break;
                    case BlockTypes.Cannon: Cannons++; break;
                    case BlockTypes.Fin: Fins++; break;
                    case BlockTypes.RetroThruster: Retros++; break;
                    case BlockTypes.Armor: Armor++; break;
                }
            }
        }

        // frob:doc docs/reference/hullbreach-bots.md#botship
        public int BlockCount => Grid.Count;

        // Seconds a live-match rebuild of the last `count` placements would take under a cooldown,
        // for deciding whether replacing a lost part is worth leaving the fight. Pre-match building is free.
        // frob:doc docs/reference/hullbreach-bots.md#botship
        public static float RebuildSeconds(int count, float buildCooldownSeconds) => math.max(0, count) * math.max(0f, buildCooldownSeconds);

        // The wire form a peer sends to ServerSimulation.Join. Weld flags are a builder-side lock and are not carried.
        // frob:doc docs/reference/hullbreach-bots.md#botship
        public ShipSnapshot ToSnapshot(ushort netId, float2 position, float rotation, float2 velocity)
        {
            var blocks = new List<SnapshotBlock>(Grid.Count);
            foreach (int key in Grid.SortedKeys.ToArray())
            {
                Grid.TryGet(key, out Block b);
                BlockKey.Unpack(key, out int x, out int y);
                blocks.Add(new SnapshotBlock((sbyte)x, (sbyte)y, b.TypeId, b.Modifiers, 0));
            }
            return new ShipSnapshot(0, netId, blocks.ToArray(), Quantization.PackPosition(position.x),
                Quantization.PackPosition(position.y), Quantization.PackAngle(rotation),
                Quantization.PackPosition(velocity.x), Quantization.PackPosition(velocity.y), 0);
        }
    }

    // Generates valid, stress-stable ship designs. The same (seed, difficulty, personality) always
    // gives the same ship; every result is checked by the game's own placement, design and structure rules.
    // frob:doc docs/reference/hullbreach-bots.md#botbuilder
    public static class BotBuilder
    {
        // Every difficulty gets the same cap; Easy simply builds worse, never smaller by decree.
        // frob:doc docs/reference/hullbreach-bots.md#botbuilder
        public const int BlockBudget = 40;

        const byte Up = 0, Right = 1, Left = 3;

        // frob:doc docs/reference/hullbreach-bots.md#botbuilder
        public static BotShip Build(ulong seed, BotDifficulty difficulty, BotPersonality personality)
        {
            var master = new BotRng(seed * 0x9E3779B97F4A7C15UL + 0x1234567UL);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                BotShip ship = Generate(master.Fork((ulong)attempt + 1), difficulty, personality);
                if (Validate(ship, out _)) return ship;
            }
            return Fallback();
        }

        // Replays the order through the real placement rules, runs the net design validator, and
        // checks stress under a burst of full thrust, full steer and fire. Null reason when valid.
        // frob:doc docs/reference/hullbreach-bots.md#botbuilder
        public static bool Validate(BotShip ship, out string reason)
        {
            reason = null;
            if (ship.BlockCount > BlockBudget) { reason = "over budget"; return false; }

            // 1. Placement order is legal block by block.
            var replay = new BlockGrid();
            foreach (BotPlacement p in ship.Order)
            {
                int key = BlockKey.Pack(p.X, p.Y);
                if (!PlacementRules.CanPlace(replay, key, p.TypeId, p.Modifiers, out PlacementVerdict why))
                { reason = "placement " + p.X + "," + p.Y + " " + why; return false; }
                replay.TryAdd(key, new Block(p.TypeId, p.Modifiers, 0, p.Welded ? Block.FlagWelded : (byte)0));
            }
            if (replay.Count != ship.Grid.Count) { reason = "order does not cover the grid"; return false; }

            // 2. The server's own join validation.
            ShipSnapshot snap = ship.ToSnapshot(1, float2.zero, 0f, float2.zero);
            DesignVerdict verdict = DesignValidator.ValidateDesign(snap);
            if (verdict != DesignVerdict.Ok) { reason = "design " + verdict; return false; }

            // 3. Capability: it must be able to move, turn and shoot.
            if (ship.Thrusters < 2 || ship.Fins < 2 || ship.Cannons < 1) { reason = "missing thrust, steering or guns"; return false; }
            ShipCapabilities caps = ShipCapabilities.Measure(MakeBody(ship));
            if (caps.MaxForwardAccel < 0.35f) { reason = "too heavy to fly"; return false; }
            if (caps.MaxAngularAccel < 0.12f) { reason = "cannot turn"; return false; }

            // 4. No block fails when the ship thrusts, steers and fires in flat space.
            if (!StressStable(ship, out reason)) return false;
            return true;
        }

        static ShipBody MakeBody(BotShip ship)
        {
            var body = new ShipBody();
            foreach (var kv in ship.Grid.All) body.Grid.TryAdd(kv.Key, kv.Value);
            body.RebuildDerivedViews();
            return body;
        }

        static bool StressStable(BotShip ship, out string reason)
        {
            reason = null;
            ShipBody body = MakeBody(ship);
            var solver = new StructuralSolver
            {
                LoadScale = StructuralSolver.DefaultLoadScale,
                MaterialStiffnessScale = StructuralSolver.DefaultMaterialStiffnessScale,
            };
            const float dt = 0.02f;
            for (int i = 0; i < 150; i++)
            {
                // Hard thrust, alternating steer, and a shot every 18 ticks.
                float steer = (i / 50) % 2 == 0 ? 1f : -1f;
                body.Step(new ShipInput(1f, steer, i % 18 == 0), dt);
                body.PendingShots.Clear();
                solver.Tick(body.Grid, body.AppliedForcesThisStep, dt);
                if (solver.BuckledBlocks.Count > 0) { reason = "buckles under thrust"; return false; }
                foreach (var kv in solver.BlockStresses)
                {
                    if (kv.Value.DuctileRatio >= 1f || kv.Value.BrittleRatio >= 1f)
                    { reason = "block overstressed under thrust"; return false; }
                }
            }
            return true;
        }

        // ---- generation ---------------------------------------------------------------------

        sealed class Plan
        {
            public int Half;            // hull half-width (1 or 2)
            public int Front;           // hull rows above the core
            public int Rear;            // hull rows below the core
            public int ThrusterPairs;
            public int CannonPairs;
            public bool CenterCannon;
            public int FinPairs;
            public bool Retro;
            public float ArmorChance;
            public bool WeldSpine;
        }

        static BotShip Generate(BotRng rng, BotDifficulty difficulty, BotPersonality personality)
        {
            Plan plan = MakePlan(rng, difficulty, personality);
            var grid = new BlockGrid();
            var order = new List<BotPlacement>();

            bool Place(int x, int y, byte type, byte mods = 0, bool welded = false)
            {
                if (grid.Count >= BlockBudget) return false;
                int key = BlockKey.Pack(x, y);
                if (!PlacementRules.CanPlace(grid, key, type, mods, out _)) return false;
                grid.TryAdd(key, new Block(type, mods, 0, welded ? Block.FlagWelded : (byte)0));
                order.Add(new BotPlacement(x, y, type, mods, welded));
                return true;
            }

            Place(0, 0, BlockTypes.Core, 0, true);

            // Hull body, nearest the core first so each cell is adjacent to something already placed.
            var cells = new List<(int x, int y)>();
            for (int y = -plan.Rear; y <= plan.Front; y++)
                for (int x = -plan.Half; x <= plan.Half; x++)
                    if (x != 0 || y != 0) cells.Add((x, y));
            cells.Sort((a, b) =>
            {
                int da = Math.Abs(a.x) + Math.Abs(a.y), db = Math.Abs(b.x) + Math.Abs(b.y);
                return da != db ? da.CompareTo(db) : (a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            });
            foreach (var (x, y) in cells)
            {
                bool frontEdge = y == plan.Front || Math.Abs(x) == plan.Half;
                bool armor = frontEdge && rng.Chance(plan.ArmorChance);
                bool spine = plan.WeldSpine && x == 0;
                Place(x, y, armor ? BlockTypes.Armor : BlockTypes.Hull, 0, spine);
            }

            // Steering fins on both flanks, anchored on the hull behind them.
            int[] finRows = { 0, -1, 1 };
            for (int i = 0; i < plan.FinPairs && i < finRows.Length; i++)
            {
                int y = finRows[i];
                Place(plan.Half + 1, y, BlockTypes.Fin, Right);
                Place(-plan.Half - 1, y, BlockTypes.Fin, Left);
            }

            // Guns on the nose row, hull row behind each so the muzzle cell ahead stays empty.
            int noseY = plan.Front + 1;
            for (int i = 0; i < plan.CannonPairs; i++)
            {
                int x = 1 + i;
                if (x > plan.Half) break;
                Place(x, noseY, BlockTypes.Cannon, Up);
                Place(-x, noseY, BlockTypes.Cannon, Up);
            }
            if (plan.CenterCannon) Place(0, noseY, BlockTypes.Cannon, Up);
            // A bare nose cell between the guns is armour when it is not a muzzle.
            if (!plan.CenterCannon && plan.CannonPairs == 1) Place(0, noseY, BlockTypes.Armor);

            // Engines on the stern row, outermost first; the cell below each stays empty for the exhaust.
            int sternY = -plan.Rear - 1;
            var thrusterX = new List<int>();
            for (int x = plan.Half; x >= 1; x--) thrusterX.Add(x);
            int placedPairs = 0;
            foreach (int x in thrusterX)
            {
                if (placedPairs >= plan.ThrusterPairs) break;
                // Keep the centre cell and its neighbours free for the retro when one is wanted.
                if (plan.Retro && x == 1) break;
                if (Place(x, sternY, BlockTypes.Thruster, Up) && Place(-x, sternY, BlockTypes.Thruster, Up)) placedPairs++;
            }
            if (plan.Retro) Place(0, sternY, BlockTypes.RetroThruster, Up);

            return new BotShip(grid, order);
        }

        static Plan MakePlan(BotRng rng, BotDifficulty difficulty, BotPersonality personality)
        {
            var plan = new Plan();
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    plan.Half = rng.Chance(0.5f) ? 1 : 2;
                    plan.Front = rng.Range(1, 3);
                    plan.Rear = 1;
                    plan.ThrusterPairs = 1;
                    plan.CannonPairs = 1;
                    plan.CenterCannon = false;
                    plan.FinPairs = 1;
                    plan.Retro = false;
                    plan.ArmorChance = rng.Range(0f, 0.3f);
                    plan.WeldSpine = rng.Chance(0.3f);
                    break;
                default:
                    // Normal and Hard fly equally capable ships; what separates them is the pilot, not the build.
                    plan.Half = 2;
                    plan.Front = rng.Range(0, 2);
                    plan.Rear = 0;
                    plan.ThrusterPairs = 2;
                    plan.CannonPairs = rng.Chance(0.6f) ? 2 : 1;
                    plan.CenterCannon = rng.Chance(0.3f);
                    plan.FinPairs = 2;
                    plan.Retro = false;
                    plan.ArmorChance = rng.Range(0.2f, 0.5f);
                    plan.WeldSpine = rng.Chance(0.5f);
                    break;
            }
            // Temperament nudges the loadout within the same budget.
            switch (personality)
            {
                case BotPersonality.Aggressive:
                    if (difficulty != BotDifficulty.Easy) plan.CannonPairs = 2;
                    plan.ArmorChance *= 0.7f;
                    break;
                case BotPersonality.Cautious:
                    plan.ArmorChance = math.min(1f, plan.ArmorChance + 0.25f);
                    plan.Retro = plan.Half == 2 && difficulty != BotDifficulty.Easy;
                    if (plan.Retro) plan.ThrusterPairs = 1;
                    break;
                case BotPersonality.Skirmisher:
                    if (difficulty != BotDifficulty.Easy) { plan.FinPairs = 3; plan.ThrusterPairs = 2; plan.Retro = false; }
                    break;
            }
            if (plan.Half == 1) { plan.CannonPairs = 1; plan.Retro = false; plan.ThrusterPairs = 1; }
            return plan;
        }

        // The known-good dart used if every attempt somehow fails validation.
        static BotShip Fallback()
        {
            var grid = new BlockGrid();
            var order = new List<BotPlacement>();
            void Add(int x, int y, byte t, byte m = 0)
            {
                grid.TryAdd(BlockKey.Pack(x, y), new Block(t, m, 0));
                order.Add(new BotPlacement(x, y, t, m, false));
            }
            Add(0, 0, BlockTypes.Core);
            foreach (int y in new[] { -1, 1 }) for (int x = -2; x <= 2; x++) Add(x, y, BlockTypes.Hull);
            Add(-1, 0, BlockTypes.Hull); Add(1, 0, BlockTypes.Hull); Add(-2, 0, BlockTypes.Hull); Add(2, 0, BlockTypes.Hull);
            Add(3, 0, BlockTypes.Fin, Right); Add(-3, 0, BlockTypes.Fin, Left);
            Add(3, -1, BlockTypes.Fin, Right); Add(-3, -1, BlockTypes.Fin, Left);
            Add(-1, 2, BlockTypes.Cannon, Up); Add(1, 2, BlockTypes.Cannon, Up);
            Add(-2, -2, BlockTypes.Thruster, Up); Add(2, -2, BlockTypes.Thruster, Up);
            return new BotShip(grid, order);
        }
    }
}
