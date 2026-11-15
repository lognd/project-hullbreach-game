using System;
using System.Collections.Generic;
using System.Text;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // One block of a saved or template design: cell, type and facing. Damage and weld flags are never part of a design.
    // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
    public readonly struct DesignBlock
    {
        public readonly int X;
        public readonly int Y;
        public readonly byte TypeId;
        public readonly byte Modifiers;

        public DesignBlock(int x, int y, byte typeId, byte modifiers)
        {
            X = x; Y = y; TypeId = typeId; Modifiers = modifiers;
        }
    }

    // A ready-made ship the build phase can apply in one click. Authored as text rows so it reads like the ship.
    // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
    public sealed class ShipTemplate
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Blurb;
        public readonly IReadOnlyList<DesignBlock> Blocks;

        public ShipTemplate(string id, string name, string blurb, string[] rows)
        {
            Id = id; Name = name; Blurb = blurb;
            Blocks = ShipTemplates.ParseRows(rows);
        }

        // Points the template spends under a price table.
        public int Cost(BlockCosts costs)
        {
            long total = 0;
            foreach (DesignBlock b in Blocks) total += costs.CostOf(b.TypeId);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        // The compact design string the client sends and the server validates.
        public string Encode() => ShipDesignCodec.Encode(Blocks);
    }

    // The starter designs. Every one is checked by tests against the placement rules, the point budget, the block
    // cap and a burst of full thrust and steer, so applying one can never hand a player a ship that breaks.
    // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
    public static class ShipTemplates
    {
        // Legend: C core, H hull, A armor, T thruster, K cannon (all facing up), R retro, < left fin, > right fin.
        // Rows run top (ahead) to bottom (astern); the C marks the origin.
        static readonly ShipTemplate[] s_all =
        {
            new ShipTemplate("balanced", "Balanced", "Two guns, four engines and armoured flanks. A good first ship.", new[]
            {
                "..K.K..",
                ".AHHHA.",
                "<AHCHA>",
                "<HHHHH>",
                ".TTHTT.",
                "...R...",
            }),
            new ShipTemplate("brawler", "Brawler", "Four guns and big engines, but thin plating. Hit first.", new[]
            {
                ".KK.KK.",
                ".HHHHH.",
                "<HHCHH>",
                "<HHHHH>",
                ".TT.TT.",
            }),
            new ShipTemplate("skirmisher", "Skirmisher", "Light and quick with extra fins. Dart in and out.", new[]
            {
                "..KAK..",
                ".<HHH>.",
                "<HHCHH>",
                "<HHHHH>",
                ".TTHTT.",
                "...R...",
            }),
            new ShipTemplate("tank", "Tank", "Heavy armour up front. Slower, but it takes a beating.", new[]
            {
                "..K.K..",
                ".AAAAA.",
                "<AHCHA>",
                "<HHHHH>",
                "..THT..",
                "...R...",
            }),
        };

        // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
        public static IReadOnlyList<ShipTemplate> All => s_all;

        // What a fresh player and the Cadet Trial start with.
        // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
        public static ShipTemplate Default => s_all[0];

        // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
        public static ShipTemplate ById(string id)
        {
            foreach (ShipTemplate t in s_all) if (t.Id == id) return t;
            return null;
        }

        internal static IReadOnlyList<DesignBlock> ParseRows(string[] rows)
        {
            int cx = -1, cy = -1;
            for (int r = 0; r < rows.Length; r++)
            {
                int c = rows[r].IndexOf('C');
                if (c >= 0) { cx = c; cy = r; }
            }
            if (cx < 0) throw new ArgumentException("template has no core");
            var list = new List<DesignBlock> { new DesignBlock(0, 0, BlockTypes.Core, 0) };
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    int x = c - cx, y = cy - r;
                    switch (rows[r][c])
                    {
                        case 'H': list.Add(new DesignBlock(x, y, BlockTypes.Hull, 0)); break;
                        case 'A': list.Add(new DesignBlock(x, y, BlockTypes.Armor, 0)); break;
                        case 'T': list.Add(new DesignBlock(x, y, BlockTypes.Thruster, 0)); break;
                        case 'K': list.Add(new DesignBlock(x, y, BlockTypes.Cannon, 0)); break;
                        case 'R': list.Add(new DesignBlock(x, y, BlockTypes.RetroThruster, 0)); break;
                        case '<': list.Add(new DesignBlock(x, y, BlockTypes.Fin, 3)); break;
                        case '>': list.Add(new DesignBlock(x, y, BlockTypes.Fin, 1)); break;
                    }
                }
            }
            return list;
        }
    }

    // Text form of a design ("x,y,type,mods;..."), used to send a template or the player's last ship to the server and
    // to remember it between sessions. Decoding is strict and bounded: it is untrusted input on the server.
    // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
    public static class ShipDesignCodec
    {
        public const int MaxChars = 480;
        public const int MaxBlocks = 64;

        // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
        public static string Encode(IEnumerable<DesignBlock> blocks)
        {
            var sb = new StringBuilder();
            foreach (DesignBlock b in blocks)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(b.X).Append(',').Append(b.Y).Append(',').Append(b.TypeId).Append(',').Append(b.Modifiers);
            }
            return sb.ToString();
        }

        // The grid's blocks, core first, so it replays in a legal order.
        // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
        public static string Encode(BlockGrid grid)
        {
            var list = new List<DesignBlock>();
            foreach (var kv in grid.All)
            {
                BlockKey.Unpack(kv.Key, out int x, out int y);
                list.Add(new DesignBlock(x, y, kv.Value.TypeId, (byte)(kv.Value.Modifiers & 3)));
            }
            list.Sort((a, b) =>
            {
                if (a.TypeId == BlockTypes.Core) return b.TypeId == BlockTypes.Core ? 0 : -1;
                if (b.TypeId == BlockTypes.Core) return 1;
                int da = Math.Abs(a.X) + Math.Abs(a.Y), db = Math.Abs(b.X) + Math.Abs(b.Y);
                if (da != db) return da.CompareTo(db);
                return a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
            });
            return Encode(list);
        }

        // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
        public static bool TryDecode(string text, out List<DesignBlock> blocks)
        {
            blocks = null;
            if (string.IsNullOrEmpty(text) || text.Length > MaxChars) return false;
            var list = new List<DesignBlock>();
            int i = 0;
            while (i < text.Length)
            {
                if (list.Count >= MaxBlocks) return false;
                if (!ReadInt(text, ref i, ',', out int x) || !ReadInt(text, ref i, ',', out int y)
                    || !ReadInt(text, ref i, ',', out int type) || !ReadInt(text, ref i, ';', out int mods)) return false;
                if (!BlockKey.InRange(x, y) || type < 0 || type >= BlockTypes.Count || mods < 0 || mods > 3) return false;
                list.Add(new DesignBlock(x, y, (byte)type, (byte)mods));
            }
            blocks = list;
            return list.Count > 0;
        }

        static bool ReadInt(string s, ref int i, char terminator, out int value)
        {
            value = 0;
            int start = i;
            bool neg = false;
            if (i < s.Length && s[i] == '-') { neg = true; i++; }
            int digits = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9' && digits < 4)
            {
                value = value * 10 + (s[i] - '0');
                i++; digits++;
            }
            if (digits == 0) { i = start; return false; }
            if (neg) value = -value;
            if (i < s.Length)
            {
                if (s[i] != terminator) return false;
                i++;
                if (terminator == ';' && i >= s.Length) return false;
            }
            else if (terminator != ';') return false;
            return true;
        }

        // Replays the design onto a fresh grid with the real placement rules, in any order that works.
        // frob:doc docs/reference/hullbreach-builder.md#shiptemplates
        public static bool TryBuildGrid(IReadOnlyList<DesignBlock> design, out BlockGrid grid, out string reason)
        {
            grid = new BlockGrid();
            reason = null;
            int cores = 0;
            foreach (DesignBlock b in design) if (b.TypeId == BlockTypes.Core) cores++;
            if (cores != 1) { reason = "needs exactly one core"; return false; }
            var pending = new List<DesignBlock>(design);
            int coreAt = pending.FindIndex(b => b.TypeId == BlockTypes.Core);
            DesignBlock core = pending[coreAt];
            if (core.X != 0 || core.Y != 0) { reason = "core must be at the origin"; return false; }
            pending.RemoveAt(coreAt);
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0, 0));
            bool progress = true;
            while (pending.Count > 0 && progress)
            {
                progress = false;
                for (int i = 0; i < pending.Count; i++)
                {
                    DesignBlock b = pending[i];
                    int key = BlockKey.Pack(b.X, b.Y);
                    if (!PlacementRules.CanPlace(grid, key, b.TypeId, b.Modifiers, out _)) continue;
                    grid.TryAdd(key, new Block(b.TypeId, b.Modifiers, 0));
                    pending.RemoveAt(i--);
                    progress = true;
                }
            }
            if (pending.Count > 0) { reason = "cannot place block at " + pending[0].X + "," + pending[0].Y; return false; }
            return true;
        }
    }
}
