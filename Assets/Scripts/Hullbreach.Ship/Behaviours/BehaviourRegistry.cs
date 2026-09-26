using System.Collections.Generic;

namespace Hullbreach.Ship.Behaviours
{
    // The (TypeId, VariantId) -> IBlockBehaviour table: the whole extension
    // mechanism. A static constructor calls RegisterDefaults so tests always see it.
    // frob:doc docs/reference/hullbreach-ship.md#behaviourregistry
    public static class BehaviourRegistry
    {
        static readonly Dictionary<(byte typeId, byte variant), IBlockBehaviour> Table
            = new Dictionary<(byte, byte), IBlockBehaviour>();

        static BehaviourRegistry()
        {
            RegisterDefaults();
        }

        // Overwrites whatever was registered there before: tests use that
        // to swap in stubs.
        // frob:doc docs/reference/hullbreach-ship.md#behaviourregistry
        public static void Register(byte typeId, byte variant, IBlockBehaviour behaviour)
        {
            Table[(typeId, variant)] = behaviour;
        }

        // Falls back to that type's variant 0 when the specific variant is
        // unregistered. Null when even variant 0 has nothing registered.
        // frob:doc docs/reference/hullbreach-ship.md#behaviourregistry
        public static IBlockBehaviour Resolve(in Hullbreach.Core.Block block)
        {
            byte variant = Hullbreach.Core.BlockVariants.Get(block.Modifiers);
            if (Table.TryGetValue((block.TypeId, variant), out var behaviour)) return behaviour;
            if (variant != 0 && Table.TryGetValue((block.TypeId, (byte)0), out var fallback)) return fallback;
            return null;
        }

        // Public so a test that mutated the table with Register can restore
        // the defaults afterward instead of leaking state across tests.
        // frob:doc docs/reference/hullbreach-ship.md#behaviourregistry
        public static void RegisterDefaults()
        {
            Register(Hullbreach.Core.BlockTypes.Thruster, 0, new ForwardThrusterBehaviour());
            Register(Hullbreach.Core.BlockTypes.Thruster, 1, new SeekingThrusterBehaviour());
            Register(Hullbreach.Core.BlockTypes.RetroThruster, 0, new RetroThrusterBehaviour());
            Register(Hullbreach.Core.BlockTypes.Fin, 0, new FinBehaviour());
            Register(Hullbreach.Core.BlockTypes.Cannon, 0, new CannonBehaviour());
            Register(Hullbreach.Core.BlockTypes.Cannon, 1, new GravityGunBehaviour());
            Register(Hullbreach.Core.BlockTypes.Cannon, 2, new AntiGravityGunBehaviour());
        }
    }
}
