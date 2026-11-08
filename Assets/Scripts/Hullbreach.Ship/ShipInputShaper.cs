using Unity.Mathematics;

namespace Hullbreach.Ship
{
    // Dead zone plus response curve for raw control axes, shared by offline,
    // online and the net demo so the ship feels the same everywhere.
    // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
    // frob:doc docs/reference/hullbreach-ship.md#shipinputshaper
    public static class ShipInputShaper
    {
        // Raw magnitudes at or below this read as zero (stick drift, key-ghost noise).
        // frob:doc docs/reference/hullbreach-ship.md#shipinputshaper
        public const float DeadZone = 0.15f;

        // Above 1 softens small deflections for fine steering; full deflection is unchanged.
        // frob:doc docs/reference/hullbreach-ship.md#shipinputshaper
        public const float ResponseExponent = 1.5f;

        // Clamps to [-1, 1], removes the dead zone (rescaling so there is no jump at its edge), applies the curve.
        // frob:doc docs/reference/hullbreach-ship.md#shipinputshaper
        public static float Shape(float raw)
        {
            if (float.IsNaN(raw)) return 0f;
            float magnitude = math.min(math.abs(raw), 1f);
            if (magnitude <= DeadZone) return 0f;

            float rescaled = (magnitude - DeadZone) / (1f - DeadZone);
            return math.sign(raw) * math.pow(rescaled, ResponseExponent);
        }

        // Shapes both axes and passes fire through untouched.
        // frob:doc docs/reference/hullbreach-ship.md#shipinputshaper
        public static ShipInput Shape(float thrustRaw, float steerRaw, bool fire)
            => new ShipInput(Shape(thrustRaw), Shape(steerRaw), fire);
    }
}
