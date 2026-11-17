using System.Collections.Generic;

namespace Hullbreach.Hud
{
    // Engine-free decluttering of floating damage numbers. Hits landing within MergeWindow seconds and MergeRadius
    // world units of a live number are folded into it (the number shows the running sum and restarts its fade); the
    // rest get a deterministic horizontal offset so simultaneous numbers fan out instead of stacking.
    // frob:doc docs/design/ux-gx.md#damage-number-declutter
    public sealed class DamageNumberAggregator
    {
        // A hit joins a number whose latest hit was this recent (seconds).
        public const float MergeWindow = 0.25f;
        // ...and this close (world units).
        public const float MergeRadius = 1.5f;
        // A number stops absorbing hits this long after it was born (seconds), so a sustained stream still shows progress.
        public const float MaxMergeAge = 0.9f;
        // Numbers born within this long of each other count as together for spreading (seconds).
        public const float SpreadWindow = 0.4f;
        // Horizontal step between fanned-out numbers (world units).
        public const float SpreadStep = 0.7f;
        // Vertical stagger per simultaneous number (cycles over 3 rows).
        public const float StaggerY = 0.45f;

        // 0, +1, -1, +2, -2 steps, repeating.
        static readonly int[] Pattern = { 0, 1, -1, 2, -2 };

        struct Entry { public int Id; public float X, Y, Sum, Born, Last; public bool Destroyed; }

        // What the view should do with a hit.
        // frob:doc docs/design/ux-gx.md#damage-number-declutter
        public readonly struct Result
        {
            // Id of the number to create or update.
            public readonly int Id;
            // True: update the existing number with this Id (new text, restart fade). False: spawn a new one.
            public readonly bool Merged;
            // Running total to show.
            public readonly float Sum;
            public readonly bool Destroyed;
            // Horizontal offset to apply to a new number (0 when merged).
            public readonly float OffsetX;
            // Small vertical stagger so numbers fanned out sideways do not overlap their neighbours' digits.
            public readonly float OffsetY;

            public Result(int id, bool merged, float sum, bool destroyed, float offsetX, float offsetY = 0f)
            { Id = id; Merged = merged; Sum = sum; Destroyed = destroyed; OffsetX = offsetX; OffsetY = offsetY; }
        }

        readonly List<Entry> _live = new List<Entry>(16);
        int _nextId = 1;

        // Number of numbers currently tracked.
        // frob:doc docs/design/ux-gx.md#damage-number-declutter
        public int LiveCount => _live.Count;

        // Forget all numbers.
        // frob:doc docs/design/ux-gx.md#damage-number-declutter
        public void Clear() { _live.Clear(); }

        // Register a hit of `damage` at (x, y) at time `now` (seconds).
        // frob:doc docs/design/ux-gx.md#damage-number-declutter
        public Result Add(float x, float y, float damage, bool destroyed, float now)
        {
            if (float.IsNaN(damage) || damage < 0f) damage = 0f;
            Prune(now);
            int best = -1; float bestD = MergeRadius * MergeRadius;
            for (int i = 0; i < _live.Count; i++)
            {
                var e = _live[i];
                if (now - e.Last > MergeWindow || now - e.Born > MaxMergeAge) continue;
                float dx = e.X - x, dy = e.Y - y, d = dx * dx + dy * dy;
                if (d <= bestD) { bestD = d; best = i; }
            }
            if (best >= 0)
            {
                var e = _live[best];
                e.Sum += damage; e.Last = now; e.Destroyed |= destroyed;
                _live[best] = e;
                return new Result(e.Id, true, e.Sum, e.Destroyed, 0f);
            }

            int together = 0;
            for (int i = 0; i < _live.Count; i++)
                if (now - _live[i].Born <= SpreadWindow) together++;
            float offset = Pattern[together % Pattern.Length] * SpreadStep;
            var n = new Entry { Id = _nextId++, X = x, Y = y, Sum = damage, Born = now, Last = now, Destroyed = destroyed };
            _live.Add(n);
            return new Result(n.Id, false, damage, destroyed, offset, (together % 3) * StaggerY);
        }

        void Prune(float now)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                if (now - _live[i].Born > MaxMergeAge + 1f) _live.RemoveAt(i);
        }
    }
}
