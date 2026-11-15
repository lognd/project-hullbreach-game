using System;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Hud
{
    // Engine-free colours and words for the build-phase structure preview (ShipStressPreview).
    // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
    public static class StressTintModel
    {
        // Risk at which a block turns amber (same line as DamageModel.CreakRatio).
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public const float AmberAt = 0.8f;

        // Risk at which a block turns red (same line as DamageModel.FailRatio).
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public const float RedAt = 1.0f;

        // Tint for one block: green below 0.8, amber to 1.0, red from 1.0. The colorblind ramp is blue / yellow / vermillion.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public static HudColor Tint(float risk, bool colorblind)
        {
            if (risk >= RedAt) return colorblind ? new HudColor(0.835f, 0.369f, 0f) : new HudColor(0.95f, 0.15f, 0.12f);
            if (risk >= AmberAt) return colorblind ? new HudColor(0.941f, 0.894f, 0.259f) : new HudColor(1f, 0.72f, 0.1f);
            return colorblind ? new HudColor(0f, 0.447f, 0.698f) : new HudColor(0.2f, 0.8f, 0.3f);
        }

        // The one-line status near the build HUD.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public static string StatusLine(StressVerdict verdict)
        {
            switch (verdict)
            {
                case StressVerdict.WillBreak: return "Structure: Will snap under full thrust";
                case StressVerdict.Marginal: return "Structure: Marginal";
                default: return "Structure: Safe";
            }
        }

        // Colour of the status line (same ramp as the tint).
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public static HudColor StatusColor(StressVerdict verdict, bool colorblind) =>
            Tint(verdict == StressVerdict.WillBreak ? RedAt : verdict == StressVerdict.Marginal ? AmberAt : 0f, colorblind);

        // 0..1 pulse, 2 Hz, for the weakest block.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public static float Pulse(float now) => 0.5f + 0.5f * (float)Math.Sin(now * 2f * Math.PI * 2.0);
    }

    // Keeps the latest preview for the local ship. Call Observe every frame with the ship's grid and a clock; it
    // re-evaluates once the grid has stopped changing for DebounceSeconds, so a burst of edits costs one solve.
    // The clock is passed in (never read here) so tests are deterministic.
    // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
    // frob:ticket 01M7GPPF37GDQMR6DZ9AB3CXBR
    public sealed class StressPreviewPresenter
    {
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public const float DebounceSeconds = 0.15f;

        // How long "Fix it" pulses the weakest block.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public const float FocusSeconds = 4f;

        readonly ShipStressPreview _preview = new ShipStressPreview();
        int _seenSignature;
        bool _haveSeen;
        bool _dirty;
        float _changedAt;
        float _focusUntil = -1f;

        // Latest result; null until the first evaluation.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public StressPreviewResult Result { get; private set; }

        // How many evaluations have run (tests use it to prove the debounce).
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public int Evaluations { get; private set; }

        // Cheap fingerprint of what the solver sees: which cell holds which type and facing.
        static int Signature(BlockGrid grid)
        {
            unchecked
            {
                int sum = 17 + grid.Count, xor = 0;
                foreach (var kv in grid.All)
                {
                    int h = kv.Key * 31 + kv.Value.TypeId * 7 + kv.Value.Modifiers;
                    h *= (int)2654435761u;
                    sum += h;
                    xor ^= h;
                }
                return sum * 31 + xor;
            }
        }

        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public void Observe(BlockGrid grid, float now)
        {
            if (grid == null) return;
            int sig = Signature(grid);
            if (!_haveSeen || sig != _seenSignature)
            {
                _haveSeen = true;
                _seenSignature = sig;
                _dirty = true;
                _changedAt = now;
            }
            if (_dirty && now - _changedAt >= DebounceSeconds)
            {
                _dirty = false;
                Result = _preview.Evaluate(grid);
                Evaluations++;
            }
        }

        // Drops the cached result (new match, or the ship is gone).
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public void Reset()
        {
            Result = null;
            _haveSeen = false;
            _dirty = false;
            _focusUntil = -1f;
        }

        // Tint for a block, or null when there is no result or the block has no risk entry.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public HudColor? TintFor(int blockKey, bool colorblind)
        {
            if (Result == null || !Result.Risk.TryGetValue(blockKey, out float risk)) return null;
            return StressTintModel.Tint(risk, colorblind);
        }

        // 0..1 pulse strength for a block: only the weakest block, only when the verdict is not Safe (or while focused).
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public float PulseFor(int blockKey, float now)
        {
            if (Result == null || blockKey != Result.WeakestKey) return 0f;
            if (Result.Verdict == StressVerdict.Safe && now >= _focusUntil) return 0f;
            return StressTintModel.Pulse(now);
        }

        // Starts the "Fix it" focus pulse on the weakest block.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public void FocusWeakest(float now) => _focusUntil = now + FocusSeconds;

        // Status line text, empty before the first result.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public string Status => Result == null ? string.Empty : StressTintModel.StatusLine(Result.Verdict);

        // READY check: one evaluation with buckling on, run once at the press (never per frame).
        // Returns the warning text when the design will break, else null. A warning, never a blocker.
        // frob:doc docs/reference/hullbreach-ship.md#stress-preview-in-the-builder
        public static string ReadyWarning(BlockGrid grid)
        {
            if (grid == null) return null;
            var r = new ShipStressPreview { IncludeBuckling = true }.Evaluate(grid);
            return r.Verdict == StressVerdict.WillBreak ? r.Warning : null;
        }
    }
}
