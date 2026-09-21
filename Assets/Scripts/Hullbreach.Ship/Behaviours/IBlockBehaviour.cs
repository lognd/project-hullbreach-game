namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// One block's per-Step logic. This is the entire extension point: a new
    /// weapon or thruster variant is one class implementing this interface
    /// plus one BehaviourRegistry.Register call: ShipBody.Step never grows
    /// a new switch case.
    /// </summary>
    public interface IBlockBehaviour
    {
        /// <summary>Advance this one block by ctx.Dt: read/ramp its throttle
        /// or cooldown, apply forces/fire shots through ctx.</summary>
        void Step(ref BlockContext ctx);
    }
}
