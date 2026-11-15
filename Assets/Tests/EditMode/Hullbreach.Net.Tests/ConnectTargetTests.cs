using System;
using System.Collections.Generic;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // Pins the name-resolving step between a ConnectRequest and the transport dial (S29-3b).
    // frob:ticket 01M5XACEYZRD4KJYBEJYTVQZRX
    public class ConnectTargetTests
    {
        const string Id = "6f1c1c1e-0a53-4d2e-9d57-3f2a6a8d9b10";

        static ConnectRequest Request(string host, ushort port = 7777) =>
            ConnectRequest.FromHandoff(new MatchHandoff(Id, host, port, "u2"), new[] { "gold" }).Value;

        [Test]
        public void NumericHost_IsUsedWithoutResolving()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectTarget.cs::Hullbreach.Net.ConnectTarget.Resolve kind="unit"
            var r = ConnectTarget.Resolve(Request("203.0.113.9"), _ => throw new InvalidOperationException("no DNS expected"));
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual("203.0.113.9:7777", r.Value.ToString());
        }

        [Test]
        public void HostName_ResolvesToFirstIPv4()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectTarget.cs::Hullbreach.Net.ConnectTarget.Resolve kind="unit"
            string asked = null;
            var r = ConnectTarget.Resolve(Request("game.example"), host =>
            {
                asked = host;
                return new[] { "::1", "198.51.100.4", "198.51.100.5" };
            });
            Assert.IsTrue(r.IsOk, r.Error);
            Assert.AreEqual("game.example", asked);
            Assert.AreEqual("198.51.100.4", r.Value.Address);
            Assert.AreEqual(7777, r.Value.Port);
        }

        [Test]
        public void ResolverFailureOrNoIPv4_IsAnError()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectTarget.cs::Hullbreach.Net.ConnectTarget.Resolve kind="unit"
            Assert.IsFalse(ConnectTarget.Resolve(Request("game.example"), _ => throw new System.Net.Sockets.SocketException()).IsOk);
            Assert.IsFalse(ConnectTarget.Resolve(Request("game.example"), _ => new[] { "::1" }).IsOk);
            Assert.IsFalse(ConnectTarget.Resolve(Request("game.example"), _ => null).IsOk);
            Assert.IsFalse(ConnectTarget.Resolve(Request("game.example"), null).IsOk);
        }

        [Test]
        public void OddHostText_IsRefusedBeforeResolving()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectTarget.cs::Hullbreach.Net.ConnectTarget.Resolve kind="unit"
            bool called = false;
            foreach (string host in new[] { "bad host", "a/b", "-x.example", "x.example.", "evil;rm", "[::1]" })
            {
                var r = ConnectTarget.Resolve(Request(host), _ => { called = true; return new[] { "1.2.3.4" }; });
                Assert.IsFalse(r.IsOk, host);
            }
            Assert.IsFalse(called);
            Assert.IsFalse(ConnectTarget.Resolve(null, _ => null).IsOk);
        }

        [Test]
        public void Ticket_CarriesTheRequestMatchId()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ConnectTarget.cs::Hullbreach.Net.ConnectTarget.Ticket kind="unit"
            var target = ConnectTarget.Resolve(Request("10.0.0.2"), null).Value;
            Assert.AreEqual(Id, target.Ticket.MatchId);
        }
    }
}
