using System;

namespace Hullbreach.Hud
{
    // Engine-free trigger logic for the local core's alerts: a red edge flash plus "CORE HIT" text and an alarm when
    // the core takes damage (the alarm has a cooldown so a burst does not machine-gun it), and a persistent
    // low-core pulse at or below LowThreshold. Feed it the core health (0..1) every frame; the view only draws.
    // frob:doc docs/design/ux-gx.md#core-hit-feedback
    public sealed class CoreAlertModel
    {
        // The core counts as low at or below this health (0..1).
        public const float LowThreshold = 0.35f;
        // Smallest health drop that counts as a hit (filters float noise).
        public const float MinDrop = 0.004f;
        // Seconds the flash and "CORE HIT" text stay up after the latest hit.
        public const float FlashSeconds = 0.7f;
        // Minimum seconds between hit alarms.
        public const float AlarmCooldown = 1.0f;
        // Seconds between the low-core beeps.
        public const float LowBeepInterval = 2.0f;

        public const string HitText = "CORE HIT";
        public const string LowText = "CORE LOW";

        bool _have;
        float _last = 1f;
        float _hitAt = float.NegativeInfinity;
        float _alarmAt = float.NegativeInfinity;
        float _beepAt = float.NegativeInfinity;
        bool _alarmDue, _beepDue;

        // True when the core is alive and at or below the low threshold.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public bool IsLow { get; private set; }

        // Size of the latest hit as a health fraction, for scaling the flash.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public float LastHit { get; private set; }

        // Forget everything (no core present: build phase, between rounds).
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public void Reset()
        {
            _have = false; _last = 1f; IsLow = false; LastHit = 0f;
            _hitAt = _alarmAt = _beepAt = float.NegativeInfinity;
            _alarmDue = _beepDue = false;
        }

        // Observe the core health at `now` (seconds). The first reading is only a baseline; a rise (repair, new
        // match) re-baselines without alerting.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public void Observe(float health, float now)
        {
            if (float.IsNaN(health)) return;
            health = Math.Min(1f, Math.Max(0f, health));
            if (!_have) { _have = true; _last = health; }
            float drop = _last - health;
            if (drop >= MinDrop)
            {
                _hitAt = now; LastHit = drop;
                if (now - _alarmAt >= AlarmCooldown) { _alarmAt = now; _alarmDue = true; }
            }
            _last = health;
            IsLow = health > 0f && health <= LowThreshold;
            if (IsLow && now - _beepAt >= LowBeepInterval)
            {
                _beepAt = now;
                if (!_alarmDue) _beepDue = true;
            }
        }

        // True once per alarm trigger: the view plays the hit alarm when this returns true.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public bool TakeAlarm() { bool d = _alarmDue; _alarmDue = false; return d; }

        // True once per low-core beep.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public bool TakeLowBeep() { bool d = _beepDue; _beepDue = false; return d; }

        // 0..1 intensity of the red edge flash, fading out after the latest hit; larger hits flash harder.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public float FlashAlpha(float now)
        {
            float age = now - _hitAt;
            if (age < 0f || age >= FlashSeconds) return 0f;
            float u = 1f - age / FlashSeconds;
            float strength = 0.55f + 0.45f * Math.Min(1f, LastHit / 0.15f);
            return u * u * strength;
        }

        // True while "CORE HIT" should be shown.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public bool HitTextVisible(float now) => now - _hitAt >= 0f && now - _hitAt < FlashSeconds;

        // 0..1 persistent pulse for the low state (0 when not low); about 1.6 Hz, between 0.25 and 0.5.
        // frob:doc docs/design/ux-gx.md#core-hit-feedback
        public float LowPulse(float now)
        {
            if (!IsLow) return 0f;
            return 0.25f + 0.25f * (0.5f + 0.5f * (float)Math.Sin(now * 1.6 * 2.0 * Math.PI));
        }
    }
}
