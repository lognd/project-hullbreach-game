namespace Hullbreach.Ship.Behaviours
{
    // One block's per-Step logic. This is the entire extension point: a new
    // weapon or thruster variant is one class implementing this interface
    // plus one BehaviourRegistry.Register call: ShipBody.Step never grows
    // a new switch case.
    // frob:doc docs/reference/hullbreach-ship.md#iblockbehaviour
    public interface IBlockBehaviour
    {
        // frob:doc docs/reference/hullbreach-ship.md#iblockbehaviour
        void Step(ref BlockContext ctx);
    }
}
