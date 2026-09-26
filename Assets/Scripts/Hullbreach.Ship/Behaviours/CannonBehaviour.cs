namespace Hullbreach.Ship.Behaviours
{
    // The stock cannon: fires the ship's own Projectile spec unchanged.
    // Cannon variant 0.
    // frob:doc docs/reference/hullbreach-ship.md#cannonbehaviour
    public sealed class CannonBehaviour : CannonBehaviourBase
    {
        // frob:doc docs/reference/hullbreach-ship.md#cannonbehaviour
        protected override ProjectileSpec BuildSpec(in BlockContext ctx) => ctx.Ship.Projectile;
    }
}
