using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class FeelModelTests
    {
        [Test]
        public void Trauma_GrowsWithDamage_AndIsBounded()
        {
            Assert.AreEqual(0f, FeelModel.TraumaForDamage(0f));
            Assert.AreEqual(0f, FeelModel.TraumaForDamage(float.NaN));
            float small = FeelModel.TraumaForDamage(10f), big = FeelModel.TraumaForDamage(120f);
            Assert.Greater(small, 0f);
            Assert.Greater(big, small);
            Assert.LessOrEqual(FeelModel.TraumaForDamage(1e6f), 0.75f + 1e-4f);
        }

        [Test]
        public void Trauma_AddClampsAndDecaysToZero()
        {
            Assert.AreEqual(1f, FeelModel.AddTrauma(0.8f, 0.9f));
            Assert.AreEqual(0.5f, FeelModel.AddTrauma(0.5f, -3f));
            Assert.AreEqual(0f, FeelModel.DecayTrauma(0.2f, 1f));
            Assert.AreEqual(0.84f, FeelModel.DecayTrauma(1f, 0.1f), 1e-4f);
        }

        [Test]
        public void ShakeOffset_ZeroAtZeroTrauma_BoundedOtherwise()
        {
            FeelModel.ShakeOffset(0f, 1f, 3.3f, out float x, out float y);
            Assert.AreEqual(0f, x); Assert.AreEqual(0f, y);
            for (float t = 0; t < 5f; t += 0.013f)
            {
                FeelModel.ShakeOffset(1f, 0.5f, t, out x, out y);
                Assert.LessOrEqual(System.Math.Abs(x), 0.5f + 1e-4f);
                Assert.LessOrEqual(System.Math.Abs(y), 0.5f + 1e-4f);
            }
        }

        [Test]
        public void DamageText_RoundsAndMarksKills()
        {
            Assert.AreEqual("24", FeelModel.DamageText(23.6f, false));
            Assert.AreEqual("30!", FeelModel.DamageText(30f, true));
            Assert.AreEqual("0", FeelModel.DamageText(-4f, false));
        }

        [Test]
        public void FadeOut_OneToZero()
        {
            Assert.AreEqual(1f, FeelModel.FadeOut(0f, 0.5f));
            Assert.AreEqual(0f, FeelModel.FadeOut(0.5f, 0.5f));
            Assert.AreEqual(0f, FeelModel.FadeOut(1f, 0f));
            Assert.Less(FeelModel.FadeOut(0.25f, 0.5f), 0.5f);
        }

        [Test]
        public void Thruster_SilentWhenIdle_LouderAndHigherWhenHarder()
        {
            Assert.AreEqual(0f, FeelModel.ThrusterVolume(0.01f));
            Assert.AreEqual(0f, FeelModel.ThrusterVolume(float.NaN));
            Assert.Greater(FeelModel.ThrusterVolume(0.1f), 0.1f);
            Assert.Greater(FeelModel.ThrusterVolume(1f), FeelModel.ThrusterVolume(0.3f));
            Assert.LessOrEqual(FeelModel.ThrusterVolume(5f), 1f);
            Assert.Greater(FeelModel.ThrusterPitch(1f), FeelModel.ThrusterPitch(0f));
        }

        [Test]
        public void StormBeep_FasterOutside_SilentWhenSafe()
        {
            Assert.AreEqual(0f, FeelModel.StormBeepInterval(false, -5f, true));
            Assert.AreEqual(0f, FeelModel.StormBeepInterval(true, 30f, false));
            float band = FeelModel.StormBeepInterval(true, 2f, true);
            float outside = FeelModel.StormBeepInterval(true, -2f, false);
            Assert.Greater(band, outside);
            Assert.Greater(outside, 0f);
        }

        [Test]
        public void Volume_MultipliesAndClamps()
        {
            Assert.AreEqual(0.25f, FeelModel.Volume(0.5f, 0.5f, 1f), 1e-5f);
            Assert.AreEqual(0f, FeelModel.Volume(float.NaN, 1f, 1f));
            Assert.AreEqual(1f, FeelModel.Volume(3f, 3f, 3f));
            Assert.AreEqual(1f, FeelModel.DistanceGain(0f, 50f));
            Assert.AreEqual(0.5f, FeelModel.DistanceGain(25f, 50f), 1e-5f);
            Assert.AreEqual(0f, FeelModel.DistanceGain(80f, 50f));
        }

        static void AssertClean(float[] s, float minSeconds, float maxSeconds)
        {
            Assert.GreaterOrEqual(s.Length, (int)(minSeconds * ProceduralAudio.SampleRate));
            Assert.LessOrEqual(s.Length, (int)(maxSeconds * ProceduralAudio.SampleRate));
            float peak = 0f;
            foreach (float v in s)
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
                peak = System.Math.Max(peak, System.Math.Abs(v));
            }
            Assert.Greater(peak, 0.2f);
            Assert.LessOrEqual(peak, 1f);
        }

        [Test]
        public void Synth_ProducesAudibleBoundedClips()
        {
            AssertClean(ProceduralAudio.Cannon(), 0.1f, 1f);
            AssertClean(ProceduralAudio.Hit(), 0.1f, 1f);
            AssertClean(ProceduralAudio.StormBeep(), 0.05f, 0.5f);
            AssertClean(ProceduralAudio.CoreAlarm(), 0.2f, 1f);
            AssertClean(ProceduralAudio.Click(), 0.01f, 0.2f);
            AssertClean(ProceduralAudio.ThrusterLoop(), 0.5f, 3f);
        }

        [Test]
        public void Synth_IsDeterministic()
        {
            CollectionAssert.AreEqual(ProceduralAudio.Cannon(), ProceduralAudio.Cannon());
        }

        [Test]
        public void ThrusterLoop_SeamIsContinuous()
        {
            var s = ProceduralAudio.ThrusterLoop();
            float seam = System.Math.Abs(s[0] - s[s.Length - 1]);
            float typical = 0f;
            for (int i = 1; i < 2000; i++) typical = System.Math.Max(typical, System.Math.Abs(s[i] - s[i - 1]));
            Assert.LessOrEqual(seam, typical * 4f + 0.05f);
        }

        [Test]
        public void Synth_ClipsAreStartAndEndQuiet()
        {
            foreach (var s in new[] { ProceduralAudio.Cannon(), ProceduralAudio.Hit(), ProceduralAudio.StormBeep(), ProceduralAudio.CoreAlarm() })
                Assert.Less(System.Math.Abs(s[0]), 0.05f);
        }
    }
}
