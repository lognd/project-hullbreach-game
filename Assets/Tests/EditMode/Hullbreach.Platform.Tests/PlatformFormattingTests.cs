using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // ToString output lands in logs, so it must stay readable and never carry a secret.
    public sealed class PlatformFormattingTests
    {
        [Test]
        public void LoginOutcome_ToString_OmitsToken()
        {
            LoginOutcome outcome = new LoginOutcome(LoginStatus.Success, "tok-SECRET", "pilot", "Signed in.");
            Assert.AreEqual("Success user='pilot' (Signed in.)", outcome.ToString());
        }

        [Test]
        public void SessionOutcome_ToString_ShowsUserAndRole()
        {
            SessionOutcome outcome = new SessionOutcome(SessionStatus.Valid, "u-1", "player", "session valid");
            Assert.AreEqual("Valid user_id='u-1' role='player' (session valid)", outcome.ToString());
        }

        [Test]
        public void AdmissionDecision_ToString_IncludesReasonOnlyWhenSet()
        {
            AdmissionDecision admitted = new AdmissionDecision(3, true, true, "pilot", "u-1", null);
            AdmissionDecision rejected = new AdmissionDecision(4, false, false, null, null, "no hello");
            Assert.AreEqual("peer 3: admitted verified name='pilot' user_id='u-1'", admitted.ToString());
            Assert.AreEqual("peer 4: rejected unverified name='' user_id='' (no hello)", rejected.ToString());
        }

        [Test]
        public void PlatformConfig_ToString_IsDescribeWithoutKey()
        {
            PlatformConfig config = new PlatformConfig(serverKey: "key-SECRET");
            Assert.AreEqual(config.Describe(), config.ToString());
            StringAssert.DoesNotContain("SECRET", config.ToString());
        }

        [Test]
        public void NullPlatformLog_DiscardsMessages()
        {
            NullPlatformLog log = NullPlatformLog.Instance;
            Assert.DoesNotThrow(() => log.Info("info"));
            Assert.DoesNotThrow(() => log.Warning("warning"));
        }
    }
}
