using System.Collections.Generic;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // Precedence (arg > env > default), validation fallbacks, and that the key stays out of logs.
    public sealed class PlatformConfigTests
    {
        static PlatformConfig Build(string[] args, Dictionary<string, string> env, RecordingPlatformLog log = null) =>
            PlatformConfig.FromEnvironment(args, name => env != null && env.TryGetValue(name, out var v) ? v : null,
                log ?? new RecordingPlatformLog());

        [Test]
        public void Defaults_WhenNothingIsSet()
        {
            var config = Build(new string[0], null);
            Assert.AreEqual("http://127.0.0.1:8000", config.BaseUrl);
            Assert.AreEqual(5.0, config.TimeoutSeconds);
            Assert.IsFalse(config.RequireAuth);
            Assert.IsFalse(config.HasServerKey);
            Assert.AreEqual("url=http://127.0.0.1:8000 timeout=5s requireAuth=no serverKey=not set", config.Describe());
        }

        [Test]
        public void CommandLineBeatsEnvironment_AndTrailingSlashIsTrimmed()
        {
            var config = Build(new[] { "game", "-hullbreachPlatformUrl", "https://api.example.test/" },
                new Dictionary<string, string> { ["HULLBREACH_PLATFORM_URL"] = "http://env.example.test" });
            Assert.AreEqual("https://api.example.test", config.BaseUrl);
        }

        [Test]
        public void EnvironmentUsed_WhenNoArgument()
        {
            var config = Build(new string[0], new Dictionary<string, string>
            {
                ["HULLBREACH_PLATFORM_URL"] = "http://env.example.test:8002",
                ["HULLBREACH_REQUIRE_PLATFORM_AUTH"] = "true",
            });
            Assert.AreEqual("http://env.example.test:8002", config.BaseUrl);
            Assert.IsTrue(config.RequireAuth);
        }

        [Test]
        public void RequireAuthFlag_AndTimeoutArgument()
        {
            var config = Build(new[] { "-hullbreachRequirePlatformAuth", "-hullbreachPlatformTimeout", "2.5" }, null);
            Assert.IsTrue(config.RequireAuth);
            Assert.AreEqual(2.5, config.TimeoutSeconds);
        }

        [Test]
        public void InvalidUrlAndTimeout_FallBackWithWarnings()
        {
            var log = new RecordingPlatformLog();
            var config = Build(new[] { "-hullbreachPlatformUrl", "ftp://nope", "-hullbreachPlatformTimeout", "-1" }, null, log);
            Assert.AreEqual(PlatformConfig.DefaultBaseUrl, config.BaseUrl);
            Assert.AreEqual(PlatformConfig.DefaultTimeoutSeconds, config.TimeoutSeconds);
            Assert.AreEqual(2, log.Lines.Count);
            StringAssert.StartsWith("WARN ", log.Lines[0]);
        }

        [Test]
        public void ServerKey_ComesOnlyFromEnvironment_AndIsNeverDescribed()
        {
            var log = new RecordingPlatformLog();
            var config = Build(new[] { "-hullbreachPlatformServerKey", "arg-secret-value" },
                new Dictionary<string, string> { ["HULLBREACH_PLATFORM_SERVER_KEY"] = "env-secret-value" }, log);
            Assert.AreEqual("env-secret-value", config.ServerKey);
            StringAssert.Contains("serverKey=set", config.Describe());
            StringAssert.DoesNotContain("secret", config.Describe());
            StringAssert.DoesNotContain("secret", log.All);
            StringAssert.Contains("-hullbreachPlatformServerKey is not supported", log.All);
        }

        [Test]
        public void ServerKeyArgument_AloneDoesNotSetAKey()
        {
            var config = Build(new[] { "-hullbreachPlatformServerKey", "arg-secret-value" }, null);
            Assert.IsFalse(config.HasServerKey);
        }
    }
}
