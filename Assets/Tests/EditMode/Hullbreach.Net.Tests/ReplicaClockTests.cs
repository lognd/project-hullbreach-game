using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // The NetCode path sends no ShipState, so the replica's clock comes from the ghost poses (storm countdown, pickups).
    public class ReplicaClockTests
    {
        [Test]
        public void SimulationTickFollowsObservedPoseTicks()
        {
            var replica = new ClientReplica();
            Assert.AreEqual(0u, replica.SimulationTick);
            replica.ObserveSimulationTick(120);
            replica.ObserveSimulationTick(100); // an older pose never rewinds it
            Assert.AreEqual(120u, replica.SimulationTick);
        }

        [Test]
        public void UseArenaGivesReplicaShipsThePlanets()
        {
            var replica = new ClientReplica();
            replica.UseArena(ArenaConfig.Defaults());
            Assert.IsNotNull(replica.PredictionGravity);
            Assert.AreEqual(ArenaConfig.Defaults().Bodies.Count, replica.PredictionGravity.PermanentCount);
        }
    }
}
