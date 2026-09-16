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
        // TODO [A6]: Decide and document the rule. Two defensible options:
        //
        //   (a) Fins are aerodynamic-ish: authority scales with current thrust,
        //       so steering requires power. Keeps the prototype's feel but
        //       makes it intentional.
        //   (b) Fins are reaction wheels / vernier thrusters: authority is
        //       independent of main thrust, so a drifting ship can still aim.
        //
        //   Whichever you pick, torque must scale with the fins' distance from
        //   the center of mass, or "where you put it matters" stops being true
        //   for fins even though it is true for thrusters.
        public static float Torque(float steerAxis, float currentThrust,
                                   float finLeverArm, float inertia)
            => throw new NotImplementedException();
    }
}
