using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hullbreach.Builder
{
    // Outcome of reading a design file: a design plus the rule problems found in it,
    // or a syntax/version error (Design null) naming the offending line.
    // frob:doc docs/reference/hullbreach-builder.md#designloadresult
    public readonly struct DesignLoadResult
    {
        // Null when the file could not be parsed at all.
        // frob:doc docs/reference/hullbreach-builder.md#designloadresult
        public readonly ShipDesign Design;

        // Empty when Design is valid; the design is never altered to make it so.
        // frob:doc docs/reference/hullbreach-builder.md#designloadresult
        public readonly IReadOnlyList<DesignProblem> Problems;

        // Null on a successful parse.
        // frob:doc docs/reference/hullbreach-builder.md#designloadresult
        public readonly string Error;

        // 1-based line of Error, 0 when it is not tied to a line.
        // frob:doc docs/reference/hullbreach-builder.md#designloadresult
        public readonly int ErrorLine;

        // frob:doc docs/reference/hullbreach-builder.md#designloadresult
        public DesignLoadResult(ShipDesign design, IReadOnlyList<DesignProblem> problems, string error, int errorLine)
        {
            Design = design;
            Problems = problems;
            Error = error;
            ErrorLine = errorLine;
        }

        // Parsed and rule-clean: safe to hand to ShipDesign.TryBuildGrid.
        // frob:doc docs/reference/hullbreach-builder.md#designloadresult
        public bool IsValid => Design != null && Problems.Count == 0;
    }

    // Reads and writes the versioned text design format (docs/ship-design-format.md).
    // Expected failures come back in DesignLoadResult, never as exceptions.
    // frob:doc docs/reference/hullbreach-builder.md#shipdesignfile
    public static class ShipDesignFile
    {
        const string Magic = "hullbreach-design";

        // Serializes a design; newlines in the name become spaces so it stays one line.
        // frob:doc docs/reference/hullbreach-builder.md#shipdesignfile
        public static string Write(ShipDesign design)
        {
            var sb = new StringBuilder();
            sb.Append(Magic).Append(' ').Append(ShipDesign.CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("name ").Append(design.Name.Replace('\r', ' ').Replace('\n', ' ')).Append('\n');
            foreach (var b in design.Blocks)
            {
                sb.Append("block ")
                  .Append(b.X.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(b.Y.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(b.TypeId.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(b.Modifiers.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(b.Damage.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }

        // Parses then validates; a syntax or version error yields no design,
        // a rule violation yields the design untouched plus its Problems.
        // frob:doc docs/reference/hullbreach-builder.md#shipdesignfile
        public static DesignLoadResult Load(string text)
        {
            if (text == null) return Fail("file is empty", 0);

            string[] lines = text.Split('\n');
            int i = 0;
            while (i < lines.Length && IsSkippable(lines[i])) i++;
            if (i == lines.Length) return Fail("file is empty", 0);

            string[] header = lines[i].TrimEnd('\r').Split(' ');
            if (header.Length != 2 || header[0] != Magic) return Fail("not a hullbreach design file", i + 1);
            if (!int.TryParse(header[1], NumberStyles.None, CultureInfo.InvariantCulture, out int version) || version < 1)
                return Fail("bad format version", i + 1);
            if (version > ShipDesign.CurrentVersion)
                return Fail($"format version {version} is newer than supported version {ShipDesign.CurrentVersion}", i + 1);

            string name = null;
            var blocks = new List<DesignBlock>();
            for (i++; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (IsSkippable(line)) continue;

                if (line.StartsWith("name ", StringComparison.Ordinal) || line == "name")
                {
                    if (name != null) return Fail("name given twice", i + 1);
                    name = line.Length > 5 ? line.Substring(5) : "";
                }
                else if (line.StartsWith("block ", StringComparison.Ordinal))
                {
                    string[] f = line.Split(' ');
                    if (f.Length != 6) return Fail("block needs: x y type modifiers damage", i + 1);
                    var v = new int[5];
                    for (int k = 0; k < 5; k++)
                    {
                        if (!int.TryParse(f[k + 1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v[k]))
                            return Fail($"'{f[k + 1]}' is not an integer", i + 1);
                    }
                    blocks.Add(new DesignBlock(v[0], v[1], v[2], v[3], v[4]));
                }
                else
                {
                    return Fail("unknown line", i + 1);
                }
            }

            var design = new ShipDesign(name ?? "", blocks);
            return new DesignLoadResult(design, ShipDesignValidator.Validate(design), null, 0);
        }

        static bool IsSkippable(string line)
        {
            string t = line.Trim();
            return t.Length == 0 || t[0] == '#';
        }

        static DesignLoadResult Fail(string error, int line)
            => new DesignLoadResult(null, Array.Empty<DesignProblem>(), error, line);
    }
}
