namespace Hullbreach.Ship.Behaviours
{
    /// <summary>The stock cannon: fires the ship's own Projectile spec
    /// unchanged. Cannon variant 0.</summary>
    public sealed class CannonBehaviour : CannonBehaviourBase
    {
        /// <summary>Uses the ship's configured Projectile spec as-is.</summary>
        protected override ProjectileSpec BuildSpec(in BlockContext ctx) => ctx.Ship.Projectile;
    }
}
