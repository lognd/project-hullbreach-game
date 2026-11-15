using System;

namespace Hullbreach.Hud
{
    // What the trial can see of the local player each frame; the view fills it from the replica and event counters.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public struct CadetObservation
    {
        public float Speed;            // u/s
        public float HeadingRadians;   // ship rotation
        public int ShotsFired;         // cumulative, by the local player
        public int BlocksPlaced;       // cumulative
        public int CratesCollected;    // cumulative
        public bool StormActive;
        public bool InsideStorm;
    }

    // The scripted Cadet Trial (UX P0-4): six steps done in order, each with a plain prompt and a done mark. Engine-free:
    // the view feeds Update once per frame and draws Prompt, Steps and Complete.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public sealed class CadetTrialModel
    {
        public const float ThrustSpeed = 6f;
        public const float SteerRadians = 1.2f;
        public const float BrakeFromSpeed = 4f;
        public const float BrakeToSpeed = 2.5f;
        public const float StormSafeSeconds = 8f;

        public static readonly string[] StepNames =
        {
            "Fly: hold W to reach 6 u/s",
            "Steer: turn with A and D",
            "Brake: build speed, then hold S to slow down",
            "Fire: press Space",
            "Build: press Tab, then click to place a block",
            "Collect a supply crate (fly through it)",
            "Storm: stay inside the ring for 8 s",
        };

        public int StepCount => StepNames.Length;
        public int Current { get; private set; }
        public bool Complete => Current >= StepNames.Length;
        public string Prompt => Complete ? "TRIAL COMPLETE - well flown, cadet" : StepNames[Current];

        float _lastHeading, _turned, _peakSpeed, _stormSeconds;
        int _shots0, _blocks0, _crates0;
        bool _primed;

        public bool IsDone(int step) => step < Current;

        // Starts a step's baselines from the current counters so only new actions count.
        void Begin(CadetObservation o)
        {
            _shots0 = o.ShotsFired; _blocks0 = o.BlocksPlaced; _crates0 = o.CratesCollected;
            _turned = 0f; _peakSpeed = 0f; _stormSeconds = 0f; _lastHeading = o.HeadingRadians;
        }

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public void Update(CadetObservation o, float dt)
        {
            if (Complete) return;
            if (!_primed) { _primed = true; Begin(o); }

            bool done = false;
            switch (Current)
            {
                case 0: done = o.Speed >= ThrustSpeed; break;
                case 1:
                    _turned += Math.Abs(Wrap(o.HeadingRadians - _lastHeading));
                    done = _turned >= SteerRadians;
                    break;
                case 2:
                    if (o.Speed > _peakSpeed) _peakSpeed = o.Speed;
                    done = _peakSpeed >= BrakeFromSpeed && o.Speed <= BrakeToSpeed;
                    break;
                case 3: done = o.ShotsFired > _shots0; break;
                case 4: done = o.BlocksPlaced > _blocks0; break;
                case 5: done = o.CratesCollected > _crates0; break;
                case 6:
                    _stormSeconds = o.StormActive && o.InsideStorm ? _stormSeconds + dt : 0f;
                    done = _stormSeconds >= StormSafeSeconds;
                    break;
            }
            _lastHeading = o.HeadingRadians;
            if (done) { Current++; if (!Complete) Begin(o); }
        }

        static float Wrap(float a)
        {
            const float twoPi = 6.2831855f;
            while (a > 3.1415927f) a -= twoPi;
            while (a < -3.1415927f) a += twoPi;
            return a;
        }
    }
}
