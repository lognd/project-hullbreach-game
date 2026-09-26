namespace Hullbreach.Ship.Behaviours
{
    // Cannon variant 1: fires an ATTRACTING gravity-well shot (positive-Mu
    // WellSpec, via IWorldSink.AddTemporaryGravity).
    // frob:doc docs/reference/hullbreach-ship.md#gravitygunbehaviour
    public sealed class GravityGunBehaviour : CannonBehaviourBase
    {
        // frob:doc docs/reference/hullbreach-ship.md#gravitygunbehaviour
        public float WellMu = 40f;

        // frob:doc docs/reference/hullbreach-ship.md#gravitygunbehaviour
        public float WellRadius = 1.5f;

        // frob:doc docs/reference/hullbreach-ship.md#gravitygunbehaviour
        public float WellSeconds = 6f;

        // frob:doc docs/reference/hullbreach-ship.md#gravitygunbehaviour
        protected override ProjectileSpec BuildSpec(in BlockContext ctx)
            => ctx.Ship.Projectile.WithGravityWell(new WellSpec(WellMu, WellRadius, WellSeconds));
    }
}
