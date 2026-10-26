using NUnit.Framework;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // Pins the direct/LAN command-line rules (S47-5): which worlds each role
    // creates and that every direct role loads the match scene itself.
    public class DirectLaunchPlanTests
    {
        static DirectLaunchPlan Parse(bool serverBuild, params string[] args) =>
            DirectLaunchPlan.Parse(args, serverBuild);

        [Test]
        public void NoArguments_IsLobby_AndLoadsNothing()
        {
            var plan = Parse(false, "Hullbreach.exe");
            Assert.AreEqual(LaunchRole.Lobby, plan.Role);
            Assert.IsFalse(plan.IsDirect);
            Assert.IsFalse(plan.LoadsGameScene);
            Assert.IsEmpty(plan.Warnings);
        }

        [Test]
        public void DirectFlag_IsHost_WithBothWorlds_OnDefaultPort()
        {
            var plan = Parse(false, "Hullbreach.exe", "-hullbreachDirect");
            Assert.AreEqual(LaunchRole.Host, plan.Role);
            Assert.AreEqual(DirectLaunchPlan.DefaultPort, plan.Port);
            Assert.IsTrue(plan.CreatesServerWorld);
            Assert.IsTrue(plan.CreatesClientWorld);
            Assert.IsTrue(plan.LoadsGameScene);
        }

        [Test]
        public void PortAlone_IsHost_OnThatPort()
        {
            var plan = Parse(false, "-hullbreachPort", "8000");
            Assert.AreEqual(LaunchRole.Host, plan.Role);
            Assert.AreEqual(8000, plan.Port);
        }

        [Test]
        public void Connect_IsClientOnly_SoItNeverOpensItsOwnServer()
        {
            var plan = Parse(false, "-hullbreachConnect", "192.168.1.23", "-hullbreachPort", "7980");
            Assert.AreEqual(LaunchRole.Client, plan.Role);
            Assert.IsFalse(plan.CreatesServerWorld);
            Assert.IsTrue(plan.CreatesClientWorld);
            Assert.AreEqual("192.168.1.23", plan.ConnectAddress);
            Assert.AreEqual(7980, plan.Port);
            Assert.IsTrue(plan.LoadsGameScene);
        }

        [Test]
        public void ConnectWinsOverDirect()
        {
            var plan = Parse(false, "-hullbreachDirect", "-hullbreachConnect", "10.0.0.5");
            Assert.AreEqual(LaunchRole.Client, plan.Role);
        }

        [Test]
        public void ServerFlag_IsServerOnly()
        {
            var plan = Parse(false, "-batchmode", "-hullbreachServer", "-hullbreachPort", "7990");
            Assert.AreEqual(LaunchRole.Server, plan.Role);
            Assert.IsTrue(plan.CreatesServerWorld);
            Assert.IsFalse(plan.CreatesClientWorld);
            Assert.AreEqual(7990, plan.Port);
            Assert.IsTrue(plan.LoadsGameScene);
        }

        [Test]
        public void ServerBuild_IsServer_EvenWithoutArguments()
        {
            var plan = Parse(true);
            Assert.AreEqual(LaunchRole.Server, plan.Role);
            Assert.AreEqual(DirectLaunchPlan.DefaultPort, plan.Port);
        }

        [Test]
        public void ServerIgnoresConnect_WithAWarning()
        {
            var plan = Parse(true, "-hullbreachConnect", "10.0.0.5");
            Assert.AreEqual(LaunchRole.Server, plan.Role);
            Assert.AreEqual(1, plan.Warnings.Count);
        }

        [Test]
        public void Localhost_MapsToLoopback()
        {
            var plan = Parse(false, "-hullbreachConnect", "localhost");
            Assert.AreEqual("127.0.0.1", plan.ConnectAddress);
            Assert.IsEmpty(plan.Warnings);
        }

        [TestCase("not-an-ip")]
        [TestCase("192.168.1")]
        [TestCase("::1")]
        [TestCase("999.1.1.1")]
        public void BadAddress_FallsBackToLoopback_WithAWarning(string address)
        {
            var plan = Parse(false, "-hullbreachConnect", address);
            Assert.AreEqual(LaunchRole.Client, plan.Role);
            Assert.AreEqual("127.0.0.1", plan.ConnectAddress);
            Assert.AreEqual(1, plan.Warnings.Count);
            StringAssert.Contains(address, plan.Warnings[0]);
        }

        [Test]
        public void MissingAddress_FallsBackToLoopback_WithAWarning()
        {
            var plan = Parse(false, "-hullbreachConnect", "-hullbreachPort", "7979");
            Assert.AreEqual("127.0.0.1", plan.ConnectAddress);
            Assert.AreEqual(1, plan.Warnings.Count);
        }

        [TestCase("0")]
        [TestCase("70000")]
        [TestCase("abc")]
        public void BadPort_FallsBackToDefault_WithAWarning(string port)
        {
            var plan = Parse(false, "-hullbreachDirect", "-hullbreachPort", port);
            Assert.AreEqual(DirectLaunchPlan.DefaultPort, plan.Port);
            Assert.AreEqual(1, plan.Warnings.Count);
        }

        [Test]
        public void ArgumentsAreCaseInsensitive()
        {
            var plan = Parse(false, "-HullbreachConnect", "10.0.0.5");
            Assert.AreEqual(LaunchRole.Client, plan.Role);
        }

        [Test]
        public void Describe_NamesRoleAndEndpoint()
        {
            Assert.AreEqual("Hullbreach direct: role=client connect=10.0.0.5:7979 scene=MultiplayerGame",
                Parse(false, "-hullbreachConnect", "10.0.0.5").Describe());
            Assert.AreEqual("Hullbreach direct: role=host listen=0.0.0.0:7979 scene=MultiplayerGame",
                Parse(false, "-hullbreachDirect").Describe());
            Assert.AreEqual("Hullbreach direct: role=server listen=0.0.0.0:7979 scene=MultiplayerGame",
                Parse(true).Describe());
            StringAssert.Contains("role=lobby", Parse(false).Describe());
        }

        [Test]
        public void MatchStatus_IsTheReplicatedShipsProofLine()
        {
            Assert.AreEqual("Hullbreach match: connected as #2; replicated ships: 2",
                DirectLaunchPlan.MatchStatus(2, 2));
        }
    }
}
