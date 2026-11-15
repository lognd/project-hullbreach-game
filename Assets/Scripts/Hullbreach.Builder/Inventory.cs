using System;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // Per-peer stock of spare blocks by type: the only thing mid-match building spends (S45).
    // Counts never go negative and each type is capped, so hoarding and underflow are both impossible.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-builder.md#inventory
    public sealed class Inventory
    {
        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public const int DefaultMaxPerType = 30;

        readonly int[] _counts = new int[BlockTypes.Count];
        // Refund credit per type in percent of one block (see AddRefundCredit).
        readonly int[] _credit = new int[BlockTypes.Count];

        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public int MaxPerType { get; }

        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public Inventory(int maxPerType = DefaultMaxPerType) { MaxPerType = Math.Max(1, maxPerType); }

        // Core is never stock, so asking for it is always 0.
        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public int Get(byte typeId) => typeId < _counts.Length && typeId != BlockTypes.Core ? _counts[typeId] : 0;

        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public int Total
        {
            get { int t = 0; for (int i = 0; i < _counts.Length; i++) t += _counts[i]; return t; }
        }

        // True when n more of the type fit under the cap.
        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public bool CanAdd(byte typeId, int n = 1) =>
            typeId < _counts.Length && typeId != BlockTypes.Core && n > 0 && _counts[typeId] + n <= MaxPerType;

        // All-or-nothing: a full shelf refuses rather than silently destroying blocks.
        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public bool TryAdd(byte typeId, int n = 1)
        {
            if (!CanAdd(typeId, n)) return false;
            _counts[typeId] += n;
            return true;
        }

        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public bool TryTake(byte typeId, int n = 1)
        {
            if (typeId >= _counts.Length || typeId == BlockTypes.Core || n <= 0 || _counts[typeId] < n) return false;
            _counts[typeId] -= n;
            return true;
        }

        // Deconstructing pays `percent` of a block back. Stock is whole blocks, so the remainder is kept as credit
        // and a block appears once the credits reach 100: two 50% refunds make one block, never a free one.
        // Returns whole blocks added (0 while the credit builds or the shelf is full).
        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public int AddRefundCredit(byte typeId, int percent)
        {
            if (typeId >= _counts.Length || typeId == BlockTypes.Core || percent <= 0) return 0;
            int credit = Math.Min(100, _credit[typeId] + Math.Min(100, percent));
            if (credit >= 100 && TryAdd(typeId)) { _credit[typeId] = credit - 100; return 1; }
            _credit[typeId] = Math.Min(99, credit);
            return 0;
        }

        // Copy of the counts, one per block type, for snapshots and HUDs.
        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public int[] ToArray() => (int[])_counts.Clone();

        // Replaces the counts (client replica); values are clamped into 0..MaxPerType.
        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public void Load(int[] counts)
        {
            for (int i = 0; i < _counts.Length; i++)
                _counts[i] = counts != null && i < counts.Length && i != BlockTypes.Core
                    ? Math.Max(0, Math.Min(MaxPerType, counts[i])) : 0;
        }

        // frob:doc docs/reference/hullbreach-builder.md#inventory
        public void Clear()
        {
            Array.Clear(_counts, 0, _counts.Length);
            Array.Clear(_credit, 0, _credit.Length);
        }
    }
}
