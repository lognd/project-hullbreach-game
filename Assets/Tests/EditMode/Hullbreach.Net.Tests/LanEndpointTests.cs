using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Pins the lobby's join-by-address rules (S28-2): what a player may type
    // and the sentence they get back when it is wrong.
    public class LanEndpointTests
    {
        static bool Try(string text, out LanEndpoint endpoint, out string error) =>
            LanEndpoint.TryParse(text, DirectLaunchPlan.DefaultPort, out endpoint, out error);

        [Test]
        public void BareAddress_UsesTheDefaultPort()
        {
            Assert.IsTrue(Try("192.168.1.23", out var e, out var error));
            Assert.IsNull(error);
            Assert.AreEqual("192.168.1.23", e.Address);
            Assert.AreEqual(DirectLaunchPlan.DefaultPort, e.Port);
        }

        [Test]
        public void AddressWithPort_UsesThatPort_AndTrimsWhitespace()
        {
            Assert.IsTrue(Try("  10.0.0.5:8123 ", out var e, out _));
            Assert.AreEqual("10.0.0.5:8123", e.ToString());
        }

        [TestCase("localhost")]
        [TestCase("LocalHost:9000")]
        public void Localhost_MapsToLoopback(string text)
        {
            Assert.IsTrue(Try(text, out var e, out _));
            Assert.AreEqual(DirectLaunchPlan.LoopbackAddress, e.Address);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Empty_AsksForAnAddress(string text)
        {
            Assert.IsFalse(Try(text, out var e, out var error));
            Assert.AreEqual(default(LanEndpoint), e);
            StringAssert.Contains("IP address", error);
        }

        [TestCase("example.com")]
        [TestCase("192.168.1")]
        [TestCase("192.168.1.256")]
        [TestCase("::1")]
        [TestCase("1.2.3.4:5:6")]
        public void NonIpv4_IsRejected_NamingTheAddress(string text)
        {
            Assert.IsFalse(Try(text, out _, out var error));
            StringAssert.Contains("IPv4", error);
        }

        [TestCase("10.0.0.5:0")]
        [TestCase("10.0.0.5:70000")]
        [TestCase("10.0.0.5:abc")]
        [TestCase("10.0.0.5:")]
        public void BadPort_IsRejected_NamingThePort(string text)
        {
            Assert.IsFalse(Try(text, out _, out var error));
            StringAssert.Contains("port", error);
        }

        [Test]
        public void CommandLine_AndLobby_AcceptTheSameAddresses()
        {
            var plan = DirectLaunchPlan.Parse(new[] { "Hullbreach.exe", "-hullbreachConnect", "localhost" }, false);
            Assert.IsTrue(LanEndpoint.TryParseAddress("localhost", out string address));
            Assert.AreEqual(plan.ConnectAddress, address);
            Assert.IsFalse(LanEndpoint.TryParseAddress("999.1.1.1", out _));
        }
    }
}
