using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Net;
using Hullbreach.World;

namespace Hullbreach.Net.Tests
{
    // Server and client build their GravityField from one GravityConfig and agree.
    public class ServerGravityConfigTests
    {
        const string Text = "max_acceleration = 30\nsurface_restitution = 0.4\nplanet = 0 -60 900 18\nplanet = 45 20 120 6\n";

        [Test]
        public void ServerField_MatchesAClientFieldBuiltFromTheSameConfig()
        {
            GravityConfig.TryParse(Text, out var config, out var error);
            Assert.IsNull(error);

            var server = new ServerSimulation(new ServerOutbox(), config);
            var client = config.BuildField();

            Assert.AreEqual(client.MaxAcceleration, server.Gravity.MaxAcceleration);
            Assert.AreEqual(client.Count, server.Gravity.Count);
            for (int i = 0; i < 20; i++)
            {
                var p = new float2(-30f + 7f * i, 5f * i - 40f);
                Assert.AreEqual(client.AccelerationAt(p), server.Gravity.AccelerationAt(p));
            }
        }

        [Test]
        public void ServerWithoutConfig_HasTheDefaultEmptyField()
        {
            var server = new ServerSimulation(new ServerOutbox());

            Assert.AreEqual(GravityConfig.Default.MaxAcceleration, server.Gravity.MaxAcceleration);
            Assert.AreEqual(0, server.Gravity.Count);
        }
    }
}
