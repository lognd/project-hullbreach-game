using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class CadetTrialModelTests
    {
        static CadetObservation Obs(float speed = 0f, float heading = 0f, int shots = 0, int blocks = 0, int crates = 0, bool storm = false, bool inside = false) =>
            new CadetObservation { Speed = speed, HeadingRadians = heading, ShotsFired = shots, BlocksPlaced = blocks, CratesCollected = crates, StormActive = storm, InsideStorm = inside };

        [Test]
        public void StepsRunInOrderAndOnlyNewActionsCount()
        {
            var m = new CadetTrialModel();
            m.Update(Obs(shots: 5, blocks: 2, crates: 1), 0.02f);
            Assert.AreEqual(0, m.Current);
            m.Update(Obs(speed: 7f, shots: 5, blocks: 2, crates: 1), 0.02f);
            Assert.AreEqual(1, m.Current);
            m.Update(Obs(speed: 3f, heading: 1.0f, shots: 5, blocks: 2, crates: 1), 0.02f);
            m.Update(Obs(speed: 3f, heading: 1.5f, shots: 5, blocks: 2, crates: 1), 0.02f);
            Assert.AreEqual(2, m.Current);
            m.Update(Obs(speed: 8f, heading: 1.5f, shots: 5, blocks: 2, crates: 1), 0.02f);
            Assert.AreEqual(2, m.Current);
            m.Update(Obs(speed: 2f, heading: 1.5f, shots: 5, blocks: 2, crates: 1), 0.02f);
            Assert.AreEqual(3, m.Current);
            m.Update(Obs(shots: 5, blocks: 2, crates: 1), 0.02f);
            Assert.AreEqual(3, m.Current, "an old shot does not count");
            m.Update(Obs(shots: 6, blocks: 2, crates: 1), 0.02f);
            Assert.AreEqual(4, m.Current);
            m.Update(Obs(shots: 6, blocks: 3, crates: 1), 0.02f);
            m.Update(Obs(shots: 6, blocks: 3, crates: 2), 0.02f);
            Assert.AreEqual(6, m.Current);
        }

        [Test]
        public void StormStepNeedsEightSecondsInsideAndResetsWhenOutside()
        {
            var m = Ready();
            for (int i = 0; i < 300; i++) m.Update(Obs(storm: true, inside: true), 0.02f);
            Assert.IsFalse(m.Complete);
            m.Update(Obs(storm: true, inside: false), 0.02f);
            for (int i = 0; i < 300; i++) m.Update(Obs(storm: true, inside: true), 0.02f);
            Assert.IsFalse(m.Complete);
            for (int i = 0; i < 150; i++) m.Update(Obs(storm: true, inside: true), 0.02f);
            Assert.IsTrue(m.Complete);
            StringAssert.Contains("COMPLETE", m.Prompt);
        }

        [Test]
        public void NoStormMeansNoProgressOnTheLastStep()
        {
            var m = Ready();
            for (int i = 0; i < 1000; i++) m.Update(Obs(storm: false, inside: true), 0.02f);
            Assert.AreEqual(6, m.Current);
        }

        // Walks the model to the storm step.
        static CadetTrialModel Ready()
        {
            var m = new CadetTrialModel();
            m.Update(Obs(), 0.02f);
            m.Update(Obs(speed: 7f), 0.02f);
            m.Update(Obs(speed: 3f, heading: 1.0f), 0.02f);
            m.Update(Obs(speed: 3f, heading: 2.5f), 0.02f);
            m.Update(Obs(speed: 8f, heading: 2.5f), 0.02f);
            m.Update(Obs(speed: 1f, heading: 2.5f), 0.02f);
            m.Update(Obs(shots: 1, heading: 2.5f), 0.02f);
            m.Update(Obs(shots: 1, blocks: 1, heading: 2.5f), 0.02f);
            m.Update(Obs(shots: 1, blocks: 1, crates: 1, heading: 2.5f), 0.02f);
            return m;
        }
    }
}
