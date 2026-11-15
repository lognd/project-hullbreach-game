using System;
using System.Globalization;

namespace Hullbreach.Hud
{
    // Engine-free numbers behind the game-feel layer (hit feedback, shake, sound levels), so the
    // tuning is unit-tested and the MonoBehaviours only play what this decides.
    // frob:doc docs/design/ui-port.md#feel-layer
    public static class FeelModel
    {
        // Shake "trauma" is 0..1; the offset scales with trauma squared so small hits are felt and big ones are loud.
        public const float MaxTrauma = 1f;

        // Trauma added by a hit of `damage` (a block-damage byte, 0..255). A cannon round does about 20-30.
        public static float TraumaForDamage(float damage)
        {
            if (float.IsNaN(damage) || damage <= 0f) return 0f;
            return (float)(0.75 * (1.0 - Math.Exp(-damage / 45.0)));
        }

        // Add and clamp.
        public static float AddTrauma(float current, float add) => Math.Min(MaxTrauma, Math.Max(0f, current) + Math.Max(0f, add));

        // Linear decay; trauma is gone after 1/rate seconds.
        public static float DecayTrauma(float trauma, float dt, float rate = 1.6f) => Math.Max(0f, trauma - Math.Max(0f, dt) * rate);

        // Camera offset in world units for a trauma at time t: cheap smooth pseudo-noise, no allocation.
        public static void ShakeOffset(float trauma, float maxOffset, float t, out float x, out float y)
        {
            float k = trauma * trauma * maxOffset;
            x = k * (float)(Math.Sin(t * 47.0) * 0.6 + Math.Sin(t * 83.0 + 1.3) * 0.4);
            y = k * (float)(Math.Sin(t * 53.0 + 2.1) * 0.6 + Math.Sin(t * 91.0 + 0.4) * 0.4);
        }

        // Floating damage number: whole points; a destroyed block gets a bang.
        public static string DamageText(float damage, bool destroyed)
        {
            int d = (int)Math.Round(Math.Max(0f, damage));
            return d.ToString(CultureInfo.InvariantCulture) + (destroyed ? "!" : string.Empty);
        }

        // 1 at birth to 0 at `life` seconds, eased out (impact flash and damage number fade).
        public static float FadeOut(float age, float life)
        {
            if (life <= 0f || age >= life) return 0f;
            float u = 1f - Math.Max(0f, age) / life;
            return u * u;
        }

        // Thruster loop volume from the mean throttle (0..1): silent when idle, never fully silent while firing.
        public static float ThrusterVolume(float meanThrottle)
        {
            if (float.IsNaN(meanThrottle) || meanThrottle < 0.02f) return 0f;
            return 0.12f + 0.88f * Math.Min(1f, meanThrottle);
        }

        // Thruster loop pitch: a little higher the harder it burns.
        public static float ThrusterPitch(float meanThrottle) => 0.8f + 0.5f * Math.Min(1f, Math.Max(0f, float.IsNaN(meanThrottle) ? 0f : meanThrottle));

        // Seconds between storm warning beeps; 0 means silent. Faster once outside the ring.
        public static float StormBeepInterval(bool stormActive, float signedDistance, bool inWarningBand)
        {
            if (!stormActive) return 0f;
            if (signedDistance < 0f) return 0.55f;
            return inWarningBand ? 1.4f : 0f;
        }

        // Effect volume for a sound heard at `distance` from the listener (the camera), linear to silence at `range`.
        public static float DistanceGain(float distance, float range)
        {
            if (range <= 0f) return 1f;
            if (distance <= 0f) return 1f;
            return Math.Max(0f, 1f - distance / range);
        }

        // Final volume for a clip: master * effects * gain, all clamped 0..1.
        public static float Volume(float master, float effects, float gain)
            => Clamp01(master) * Clamp01(effects) * Clamp01(gain);

        static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));
    }

    // Code-synthesised sound effects, so the game ships audio with no asset licences. Mono float samples in -1..1.
    // frob:doc docs/design/ui-port.md#feel-layer
    public static class ProceduralAudio
    {
        public const int SampleRate = 32000;

        // Deterministic noise so a sound is the same every run (and testable).
        sealed class Rng
        {
            uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next() // -1..1
            {
                _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
                return (_s & 0xFFFFFF) / (float)0x7FFFFF - 1f;
            }
        }

        static float[] Make(float seconds) => new float[(int)(seconds * SampleRate)];

        static void Normalize(float[] s, float peak)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++) max = Math.Max(max, Math.Abs(s[i]));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        // Cannon: noise crack over a falling sine thump.
        public static float[] Cannon()
        {
            var s = Make(0.30f); var r = new Rng(11); float lp = 0f, phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float freq = 55f + 190f * (float)Math.Exp(-t * 22f);
                phase += 2f * (float)Math.PI * freq / SampleRate;
                lp += 0.35f * (r.Next() - lp);
                float env = (float)Math.Exp(-t * 14f);
                float attack = Math.Min(1f, t * 600f);
                s[i] = attack * env * (0.65f * (float)Math.Sin(phase) + 0.55f * lp);
            }
            Normalize(s, 0.85f); return s;
        }

        // Hit: shorter, lower and crunchier than the cannon.
        public static float[] Hit()
        {
            var s = Make(0.22f); var r = new Rng(23); float lp = 0f, phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float freq = 70f + 90f * (float)Math.Exp(-t * 30f);
                phase += 2f * (float)Math.PI * freq / SampleRate;
                lp += 0.6f * (r.Next() - lp);
                float env = (float)Math.Exp(-t * 20f);
                s[i] = Math.Min(1f, t * 900f) * env * (0.5f * (float)Math.Sin(phase) + 0.8f * lp);
            }
            Normalize(s, 0.9f); return s;
        }

        // Thruster: low-passed noise rumble; the tail is cross-faded into the head so it loops with no click.
        public static float[] ThrusterLoop()
        {
            const float seconds = 1.2f;
            int n = (int)(seconds * SampleRate);
            int fade = SampleRate / 8;
            var raw = new float[n + fade]; var r = new Rng(37); float a = 0f, b = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                a += 0.05f * (r.Next() - a);
                b += 0.02f * (r.Next() - b);
                raw[i] = a * 0.8f + b * 1.4f;
            }
            var s = new float[n];
            for (int i = 0; i < n; i++) s[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float u = i / (float)fade; // blend the extra samples after the end into the start
                s[i] = s[i] * u + raw[n + i] * (1f - u);
            }
            Normalize(s, 0.7f); return s;
        }

        // Storm warning: one clean beep.
        public static float[] StormBeep()
        {
            var s = Make(0.14f); float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                phase += 2f * (float)Math.PI * 880f / SampleRate;
                float env = Math.Min(1f, t * 400f) * Math.Min(1f, (0.14f - t) * 200f);
                s[i] = env * (float)Math.Sin(phase);
            }
            Normalize(s, 0.5f); return s;
        }

        // Core under attack: a two-tone buzz, hard to ignore.
        public static float[] CoreAlarm()
        {
            var s = Make(0.5f); float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float freq = ((int)(t * 8f) % 2 == 0) ? 520f : 390f;
                phase += 2f * (float)Math.PI * freq / SampleRate;
                float sq = Math.Sign((float)Math.Sin(phase)) * 0.5f + (float)Math.Sin(phase) * 0.5f;
                float env = Math.Min(1f, t * 300f) * Math.Min(1f, (0.5f - t) * 40f);
                s[i] = env * sq;
            }
            Normalize(s, 0.55f); return s;
        }

        // UI click: a tiny tick.
        public static float[] Click()
        {
            var s = Make(0.045f); float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                phase += 2f * (float)Math.PI * 1500f / SampleRate;
                s[i] = (float)Math.Exp(-t * 90f) * (float)Math.Sin(phase);
            }
            Normalize(s, 0.45f); return s;
        }
    }
}
