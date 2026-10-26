using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Hullbreach.Core;
using Hullbreach.Platform;

namespace Hullbreach.Net
{
    // Engine-free, versioned JSON form of a BlockGrid ship design (S35-1); load validates everything.
    // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
    public static class BlockGridDesign
    {
        // Bumped on any incompatible change to the file layout.
        // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
        public const int FormatVersion = 1;

        // Same cap the server applies to a ShipSnapshot (INV-003).
        // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
        public const int MaxBlocks = ShipSnapshot.MaxBlocks;

        // Refuses oversized text before parsing; a full 4096-block design is ~250 KiB.
        // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
        public const int MaxJsonChars = 1 << 20;

        // The parser recurses per nesting level, so deep input is refused up front.
        const int MaxNesting = 6;

        // Writes the grid as a version-1 JSON object, blocks sorted by key so output is deterministic.
        // Damage is deliberately not saved: a loaded design starts intact.
        // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
        public static string Serialize(BlockGrid grid)
        {
            var sb = new StringBuilder(64 + grid.Count * 40);
            sb.Append("{\"version\":").Append(FormatVersion.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"blocks\":[");
            var keys = grid.SortedKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                grid.TryGet(keys[i], out Block b);
                BlockKey.Unpack(keys[i], out int x, out int y);
                if (i > 0) sb.Append(',');
                sb.Append("{\"x\":").Append(x.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"y\":").Append(y.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"type\":").Append(PlatformJson.Quote(BlockTypes.Get(b.TypeId).Name));
                sb.Append(",\"mods\":").Append(b.Modifiers.ToString(CultureInfo.InvariantCulture));
                sb.Append('}');
            }
            return sb.Append("]}").ToString();
        }

        // Parses and validates; any problem comes back as an error message, never an exception.
        // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
        public static ConfigResult<BlockGrid> Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return Fail("design is empty");
            if (json.Length > MaxJsonChars)
                return Fail("design is larger than " + MaxJsonChars.ToString(CultureInfo.InvariantCulture) + " characters");
            if (TooDeep(json)) return Fail("design is nested too deeply");
            if (!PlatformJson.TryParseObject(json, out var root)) return Fail("design is not a JSON object");

            if (!root.TryGetValue("version", out object rawVersion)) return Fail("version is required");
            if (!(rawVersion is double dv) || dv != FormatVersion)
                return Fail("unsupported design version (this build reads version "
                    + FormatVersion.ToString(CultureInfo.InvariantCulture) + ")");

            if (!root.TryGetValue("blocks", out object rawBlocks)) return Fail("blocks is required");
            var list = rawBlocks as List<object>;
            if (list == null) return Fail("blocks must be an array");
            if (list.Count == 0) return Fail("design has no blocks");
            if (list.Count > MaxBlocks)
                return Fail("design has more than " + MaxBlocks.ToString(CultureInfo.InvariantCulture) + " blocks");

            var grid = new BlockGrid();
            for (int i = 0; i < list.Count; i++)
            {
                string err = AddBlock(grid, list[i] as Dictionary<string, object>,
                    "blocks[" + i.ToString(CultureInfo.InvariantCulture) + "]");
                if (err != null) return Fail(err);
            }

            if (!grid.CoreKey.HasValue) return Fail("design has no core block");
            var detached = new List<int>();
            Connectivity.FindDetached(grid, detached);
            if (detached.Count > 0)
            {
                BlockKey.Unpack(detached[0], out int dx, out int dy);
                return Fail("design is not connected to the core (" + detached.Count.ToString(CultureInfo.InvariantCulture)
                    + " detached, first at " + dx.ToString(CultureInfo.InvariantCulture) + ","
                    + dy.ToString(CultureInfo.InvariantCulture) + ")");
            }
            return ConfigResult<BlockGrid>.Ok(grid);
        }

        // Reads a file and parses it; an unreadable file is an error, not an exception.
        // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
        public static ConfigResult<BlockGrid> LoadFile(string path)
        {
            string text;
            try
            {
                // UTF-8 can take up to 4 bytes per char; this only stops absurd files before reading.
                if (new FileInfo(path).Length > MaxJsonChars * 4L)
                    return Fail("design file '" + path + "' is too large");
                text = File.ReadAllText(path);
            }
            catch (Exception e) when (IsIoError(e))
            {
                return Fail("cannot read design '" + path + "': " + e.Message);
            }
            return Parse(text);
        }

        // Writes Serialize(grid) to path; returns null on success or an error message.
        // frob:doc docs/reference/hullbreach-net.md#blockgriddesign
        public static string SaveFile(string path, BlockGrid grid)
        {
            try
            {
                File.WriteAllText(path, Serialize(grid));
                return null;
            }
            catch (Exception e) when (IsIoError(e))
            {
                return "cannot write design '" + path + "': " + e.Message;
            }
        }

        static bool IsIoError(Exception e) => e is IOException || e is UnauthorizedAccessException
            || e is ArgumentException || e is NotSupportedException;

        static ConfigResult<BlockGrid> Fail(string message) => ConfigResult<BlockGrid>.Fail(message);

        // Returns an error text or null; TryAdd refuses only a duplicate cell or a second core here.
        static string AddBlock(BlockGrid grid, Dictionary<string, object> o, string at)
        {
            if (o == null) return at + " must be an object";
            if (!TryInt(o, "x", BlockKey.Min, BlockKey.Max, out int x))
                return at + ".x must be an integer in " + BlockKey.Min + ".." + BlockKey.Max;
            if (!TryInt(o, "y", BlockKey.Min, BlockKey.Max, out int y))
                return at + ".y must be an integer in " + BlockKey.Min + ".." + BlockKey.Max;
            if (!o.TryGetValue("type", out object rawType) || !(rawType is string name))
                return at + ".type must be a block type name";
            byte typeId = 0;
            bool known = false;
            for (int t = 0; t < BlockTypes.Count && !known; t++)
                if (BlockTypes.Get((byte)t).Name == name) { typeId = (byte)t; known = true; }
            if (!known) return at + ".type '" + name + "' is not a known block type";

            int mods = 0;
            if (o.ContainsKey("mods") && !TryInt(o, "mods", 0, 255, out mods))
                return at + ".mods must be an integer in 0..255";

            int key = BlockKey.Pack(x, y);
            if (grid.Contains(key)) return at + " overlaps another block at " + x + "," + y;
            if (typeId == BlockTypes.Core && grid.CoreKey.HasValue) return at + " is a second core block";
            grid.TryAdd(key, new Block(typeId, (byte)mods));
            return null;
        }

        // True when the key holds a whole number inside [min, max].
        static bool TryInt(Dictionary<string, object> o, string key, int min, int max, out int value)
        {
            value = 0;
            if (!o.TryGetValue(key, out object raw) || !(raw is double d)) return false;
            if (double.IsNaN(d) || d < min || d > max || d != Math.Floor(d)) return false;
            value = (int)d;
            return true;
        }

        // Counts { and [ nesting outside string literals.
        static bool TooDeep(string json)
        {
            int depth = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '{' || c == '[')
                {
                    if (++depth > MaxNesting) return true;
                }
                else if ((c == '}' || c == ']') && depth > 0) depth--;
            }
            return false;
        }
    }
}
