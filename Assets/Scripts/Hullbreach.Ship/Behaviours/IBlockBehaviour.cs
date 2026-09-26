namespace Hullbreach.Ship.Behaviours
{
    // One block's per-Step logic: the entire extension point for a new
    // weapon/thruster variant (see BehaviourRegistry).
    // frob:doc docs/reference/hullbreach-ship.md#iblockbehaviour
    public interface IBlockBehaviour
    {
        // frob:doc docs/reference/hullbreach-ship.md#iblockbehaviour
        void Step(ref BlockContext ctx);
    }
}
