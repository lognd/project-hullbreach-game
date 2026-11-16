using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class CoreAlertModelTests
    {
        [Test]
        public void FirstReading_IsOnlyABaseline()
        {
            var m = new CoreAlertModel();
            m.Observe(0.6f, 10f);
            Assert.IsFalse(m.TakeAlarm());
            Assert.AreEqual(0f, m.FlashAlpha(10f));
            Assert.IsFalse(m.HitTextVisible(10f));
        }

        [Test]
        public void Damage_FlashesShowsTextAndAlarmsOnce()
        {
            var m = new CoreAlertModel();
            m.Observe(1f, 0f);
            m.Observe(0.9f, 1f);
            Assert.IsTrue(m.TakeAlarm());
            Assert.IsFalse(m.TakeAlarm(), "consumed");
            Assert.Greater(m.FlashAlpha(1f), 0.5f);
            Assert.IsTrue(m.HitTextVisible(1.2f));
            Assert.AreEqual(0f, m.FlashAlpha(1f + CoreAlertModel.FlashSeconds + 0.01f));
            Assert.IsFalse(m.HitTextVisible(2f));
        }

        [Test]
        public void TinyNoiseAndHealing_DoNotTrigger()
        {
            var m = new CoreAlertModel();
            m.Observe(0.8f, 0f);
            m.Observe(0.7999f, 1f);
            Assert.IsFalse(m.TakeAlarm());
            m.Observe(1f, 2f);
            Assert.IsFalse(m.TakeAlarm());
            Assert.AreEqual(0f, m.FlashAlpha(2f));
            m.Observe(0.9f, 3f);
            Assert.IsTrue(m.TakeAlarm(), "a hit after healing counts from the new baseline");
        }

        [Test]
        public void AlarmCooldown_SuppressesBurstsButFlashRefreshes()
        {
            var m = new CoreAlertModel();
            m.Observe(1f, 0f);
            m.Observe(0.9f, 1f); Assert.IsTrue(m.TakeAlarm());
            m.Observe(0.8f, 1.3f); Assert.IsFalse(m.TakeAlarm(), "within the cooldown");
            Assert.Greater(m.FlashAlpha(1.3f), 0.5f, "the flash still restarts");
            m.Observe(0.7f, 1f + CoreAlertModel.AlarmCooldown + 0.1f);
            Assert.IsTrue(m.TakeAlarm());
        }

        [Test]
        public void BiggerHit_FlashesHarder()
        {
            var a = new CoreAlertModel(); a.Observe(1f, 0f); a.Observe(0.98f, 1f);
            var b = new CoreAlertModel(); b.Observe(1f, 0f); b.Observe(0.7f, 1f);
            Assert.Greater(b.FlashAlpha(1f), a.FlashAlpha(1f));
            Assert.LessOrEqual(b.FlashAlpha(1f), 1f);
        }

        [Test]
        public void Low_AtOrBelowThreshold_PulsesPersistently()
        {
            var m = new CoreAlertModel();
            m.Observe(0.5f, 0f);
            Assert.IsFalse(m.IsLow);
            Assert.AreEqual(0f, m.LowPulse(1f));
            m.Observe(CoreAlertModel.LowThreshold, 1f);
            Assert.IsTrue(m.IsLow);
            for (float t = 1f; t < 5f; t += 0.1f)
                Assert.GreaterOrEqual(m.LowPulse(t), 0.25f - 1e-4f, "never fully off while low");
        }

        [Test]
        public void Low_BeepsOnItsOwnInterval_NotOnTopOfAHitAlarm()
        {
            var m = new CoreAlertModel();
            m.Observe(0.5f, 0f);
            m.Observe(0.3f, 1f); // a hit into the low zone: the alarm plays, not a beep
            Assert.IsTrue(m.TakeAlarm());
            Assert.IsFalse(m.TakeLowBeep());
            m.Observe(0.3f, 1f + CoreAlertModel.LowBeepInterval + 0.1f);
            Assert.IsTrue(m.TakeLowBeep());
            m.Observe(0.3f, 1f + CoreAlertModel.LowBeepInterval + 0.2f);
            Assert.IsFalse(m.TakeLowBeep());
        }

        [Test]
        public void DeadCore_IsNotLow_ButTheKillingHitAlerts()
        {
            var m = new CoreAlertModel();
            m.Observe(0.2f, 0f);
            m.Observe(0f, 1f);
            Assert.IsFalse(m.IsLow);
            Assert.IsTrue(m.TakeAlarm());
        }

        [Test]
        public void Reset_ForgetsTheBaseline()
        {
            var m = new CoreAlertModel();
            m.Observe(0.2f, 0f);
            m.Reset();
            m.Observe(1f, 5f);
            Assert.IsFalse(m.IsLow);
            Assert.IsFalse(m.TakeAlarm());
        }

        [Test]
        public void NaN_IsIgnored()
        {
            var m = new CoreAlertModel();
            m.Observe(1f, 0f);
            m.Observe(float.NaN, 1f);
            Assert.IsFalse(m.TakeAlarm());
        }
    }
}
