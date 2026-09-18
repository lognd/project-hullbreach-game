using System;
using Unity.Mathematics;

namespace Hullbreach.Ship
{
    /// <summary>
    /// How steering input becomes torque -- S39 criterion 3, "control fins
    /// change turning behavior in a way a player can feel".
    ///
    /// THE THING BEING REPLACED: the prototype did
    ///
    ///     rb.AddTorque(Input.GetAxis("Horizontal") * -0.1f * rb.totalForce.y);
    ///
    /// rb.totalForce is the force accumulated on the body THIS PHYSICS STEP, so
    /// with the thrusters off it is near zero and the ship will not turn at all
    /// until you hold thrust. That may be a deliberate "you can only steer
    /// under power" rule, or it may be a leftover -- but it should be a
    /// decision, and this is where it gets made.
    /// </summary>
    public static class SteeringModel
    {
        /// <summary>Torque authority per unit lever arm, with no thrust
        /// applied. Tunable; chosen so a bare ship visibly turns within a
        /// couple of seconds at max steer input.</summary>
        public const float FinAuthority = 4.0f;

        /// <summary>Fraction of FinAuthority added on top at full thrust. Kept
        /// small and additive (not multiplicative) so steering never drops to
        /// zero just because the main engine is off.</summary>
        public const float ThrustBonusFraction = 0.25f;

        /// <summary>
        /// DECISION (S39 criterion 3): option (b), reaction-wheel / vernier
        /// fins. Authority is independent of main thrust so a ship coasting
        /// with the engine off can still be aimed -- a drifting ship that
        /// cannot turn to compensate is not "handles like it was built", it is
        /// "handles like a brick". currentThrust still contributes a modest
        /// (+25% at full thrust) bonus, both to keep the parameter meaningful
        /// and because a ship under power plausibly has more reaction mass /
        /// power routed to its attitude thrusters.
        ///
        /// Torque still scales with finLeverArm, so WHERE the steering blocks
        /// sit keeps mattering exactly as it does for main thrusters.
        /// `inertia` is accepted for callers that want to convert this into an
        /// angular acceleration themselves, but is not used here -- Torque
        /// returns a torque, not an alpha.
        /// </summary>
        public static float Torque(float steerAxis, float currentThrust,
                                   float finLeverArm, float inertia)
        {
            float authority = FinAuthority * (1f + ThrustBonusFraction * currentThrust);
            return steerAxis * authority * finLeverArm;
        }
    }
}
