namespace Hullbreach.Ship.Behaviours
{
    // Cannon variant 2: fires a REPULSING gravity-well shot (negative-Mu
    // WellSpec).
    // frob:doc docs/reference/hullbreach-ship.md#antigravitygunbehaviour
    public sealed class AntiGravityGunBehaviour : CannonBehaviourBase
    {
        // Negative: repels rather than attracts.
        // frob:doc docs/reference/hullbreach-ship.md#antigravitygunbehaviour
        public float WellMu = -40f;

        // frob:doc docs/reference/hullbreach-ship.md#antigravitygunbehaviour
        public float WellRadius = 1.5f;

        // frob:doc docs/reference/hullbreach-ship.md#antigravitygunbehaviour
        public float WellSeconds = 6f;

        // frob:doc docs/reference/hullbreach-ship.md#antigravitygunbehaviour
        protected override ProjectileSpec BuildSpec(in BlockContext ctx)
            => ctx.Ship.Projectile.WithGravityWell(new WellSpec(WellMu, WellRadius, WellSeconds));
    }
}
