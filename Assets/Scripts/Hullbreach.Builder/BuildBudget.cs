using System;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // Why a budget refused a build; None means it did not.
    // frob:doc docs/reference/hullbreach-builder.md#builddenial
    public enum BuildDenial
    {
        None,
        Cooldown,
        InsufficientCredits,
    }

    // The mid-match build economy numbers in one place so balance changes are one edit
    // (S34-3). Costs themselves come from BlockPalette.
    // frob:doc docs/reference/hullbreach-builder.md#buildtuning
    public readonly struct BuildTuning
    {
        // Credits available the moment a match starts (about two hulls and a cannon).
        // frob:doc docs/reference/hullbreach-builder.md#buildtuning
        public readonly float StartingCredits;

        // Cap on banked credits, so waiting cannot buy a whole new ship at once.
        // frob:doc docs/reference/hullbreach-builder.md#buildtuning
        public readonly float MaxCredits;

        // Credits regained per second; at 1.0 a hull costs one second and a cannon five.
        // frob:doc docs/reference/hullbreach-builder.md#buildtuning
        public readonly float RefillPerSecond;

        // Minimum seconds between two placements whatever the credits, so a bank cannot be dumped in one frame.
        // frob:doc docs/reference/hullbreach-builder.md#buildtuning
        public readonly float CooldownSeconds;

        // frob:doc docs/reference/hullbreach-builder.md#buildtuning
        public BuildTuning(float startingCredits, float maxCredits, float refillPerSecond, float cooldownSeconds)
        {
            StartingCredits = startingCredits;
            MaxCredits = maxCredits;
            RefillPerSecond = refillPerSecond;
            CooldownSeconds = cooldownSeconds;
        }

        // The shipped numbers; see docs/reference/hullbreach-builder.md#buildtuning.
        // frob:doc docs/reference/hullbreach-builder.md#buildtuning
        public static readonly BuildTuning MidMatch = new BuildTuning(10f, 20f, 1f, 0.5f);
    }

    // Credits plus a placement cooldown, advanced only by Tick so a client
    // session and an authoritative server agree frame for frame. No engine, no clock.
    // frob:doc docs/reference/hullbreach-builder.md#buildbudget
    public sealed class BuildBudget
    {
        readonly BuildTuning _tuning;

        // Pre-match building: every request is allowed and nothing is charged.
        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public static BuildBudget Unlimited() => new BuildBudget(default, true);

        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public BuildBudget(BuildTuning tuning) : this(tuning, false)
        {
        }

        BuildBudget(BuildTuning tuning, bool unlimited)
        {
            _tuning = tuning;
            IsUnlimited = unlimited;
            Credits = tuning.StartingCredits;
        }

        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public bool IsUnlimited { get; }

        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public float Credits { get; private set; }

        // Seconds until the next placement is allowed; 0 when ready.
        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public float CooldownRemaining { get; private set; }

        // Advances refill and cooldown by dt seconds (negative dt is ignored).
        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public void Tick(float dt)
        {
            if (IsUnlimited || dt <= 0f) return;
            CooldownRemaining = Math.Max(0f, CooldownRemaining - dt);
            Credits = Math.Min(_tuning.MaxCredits, Credits + _tuning.RefillPerSecond * dt);
        }

        // Whether a block of this type could be bought now, without charging.
        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public BuildDenial Check(byte typeId)
        {
            if (IsUnlimited) return BuildDenial.None;
            if (CooldownRemaining > 0f) return BuildDenial.Cooldown;
            if (Credits < BlockPalette.CostOf(typeId)) return BuildDenial.InsufficientCredits;
            return BuildDenial.None;
        }

        // Charges and starts the cooldown, or says why not and changes nothing.
        // frob:doc docs/reference/hullbreach-builder.md#buildbudget
        public bool TryCharge(byte typeId, out BuildDenial denial)
        {
            denial = Check(typeId);
            if (denial != BuildDenial.None) return false;
            if (IsUnlimited) return true;

            Credits -= BlockPalette.CostOf(typeId);
            CooldownRemaining = _tuning.CooldownSeconds;
            return true;
        }
    }
}
