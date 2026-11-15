using Unity.Mathematics;

namespace Hullbreach.Ship
{
    // Every knob of how the ship feels to fly, in one struct shared by the server, the client prediction
    // and the offline ShipController so all three behave identically. Legacy() is the old raw physics;
    // Default() is the tuned feel.
    // frob:doc docs/reference/hullbreach-ship.md#flighttuning
    public struct FlightTuning
    {
        // Steer commands a spin rate and a gyro holds it; releasing steer holds the heading.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public bool AssistEnabled;
        // Spin rate (rad/s) at full steer for a ship of ReferenceInertia.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float MaxRateAtReference;
        // Lower and upper bound of the full-steer spin rate, whatever the ship size.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float MinRate;
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float MaxRate;
        // Moment of inertia the rate cap is scaled from; big ships turn slower, small ones faster.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float ReferenceInertia;
        // Gyro torque per unit of ship mass at a full rate error.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float GyroTorquePerMass;
        // Rate error (rad/s) at which the gyro reaches full torque; below it the torque is proportional.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float GyroErrorSaturation;
        // Multiplier on the upgrade-level thruster ramp when throttling up (higher is faster).
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float ThrusterRampUpScale;
        // Multiplier when throttling down; below RampUpScale so thrust tails off a little slower than it bites.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float ThrusterRampDownScale;
        // Fin ramp times in seconds (0 keeps the legacy shared ramp).
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float FinRampUpSeconds;
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float FinRampDownSeconds;
        // Holding back kills velocity (never reverses it) at this multiple of the retro thrusters' own acceleration.
        // 0 disables the brake assist.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float BrakeGain;
        // Below this speed (u/s) the brake just stops the ship.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float BrakeStopSpeed;
        // A fire press waits this long for the cannon to come off cooldown; 0 disables buffering.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float FireBufferSeconds;

        // The old physics: no assist, symmetric upgrade ramps, no brake, no buffer.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public static FlightTuning Legacy() => new FlightTuning
        {
            AssistEnabled = false,
            MaxRateAtReference = 2.5f, MinRate = 0.6f, MaxRate = 3.5f, ReferenceInertia = 19.79f,
            GyroTorquePerMass = 4.5f, GyroErrorSaturation = 0.5f,
            ThrusterRampUpScale = 1f, ThrusterRampDownScale = 1f,
            FinRampUpSeconds = 0f, FinRampDownSeconds = 0f,
            BrakeGain = 0f, BrakeStopSpeed = 0.15f, FireBufferSeconds = 0f,
        };

        // The tuned feel used online and offline.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public static FlightTuning Default()
        {
            FlightTuning t = Legacy();
            t.AssistEnabled = true;
            t.ThrusterRampUpScale = 2.5f;          // level 0: 1.0 s -> 0.4 s to full
            t.ThrusterRampDownScale = 5f / 3f;     // 1.5x slower off than on
            t.FinRampUpSeconds = 0.15f;
            t.FinRampDownSeconds = 0.10f;
            t.BrakeGain = 22f;
            t.FireBufferSeconds = 0.15f;
            return t;
        }

        // Null when valid, otherwise why not.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public string Validate()
        {
            if (!(MinRate > 0f) || MaxRate < MinRate) return "rate bounds must satisfy 0 < MinRate <= MaxRate";
            if (MaxRateAtReference <= 0f || ReferenceInertia <= 0f) return "MaxRateAtReference and ReferenceInertia must be positive";
            if (GyroTorquePerMass < 0f || GyroErrorSaturation <= 0f) return "gyro needs torque >= 0 and a positive saturation";
            if (ThrusterRampUpScale <= 0f || ThrusterRampDownScale <= 0f) return "ramp scales must be positive";
            if (FinRampUpSeconds < 0f || FinRampDownSeconds < 0f) return "fin ramp seconds cannot be negative";
            if (BrakeGain < 0f || BrakeStopSpeed < 0f) return "brake values cannot be negative";
            if (FireBufferSeconds < 0f || FireBufferSeconds > 0.5f) return "FireBufferSeconds must be within 0..0.5";
            return null;
        }

        // Full-steer spin rate for a ship of this inertia: sqrt scaling from the reference, clamped.
        // frob:doc docs/reference/hullbreach-ship.md#flighttuning
        public float MaxRateFor(float inertia)
        {
            if (inertia <= 0f) return MaxRate;
            return math.clamp(MaxRateAtReference * math.sqrt(ReferenceInertia / inertia), MinRate, MaxRate);
        }
    }
}
