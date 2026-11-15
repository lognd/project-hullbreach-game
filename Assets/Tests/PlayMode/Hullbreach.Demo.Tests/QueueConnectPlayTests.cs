using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hullbreach.Net;
using Hullbreach.NetCode.Entities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hullbreach.Demo.Tests
{
    // Play-mode proof that a found match dials the assigned server and loads the game scene (S29-3b criterion 2):
    // the real LobbyScene, the real HullbreachQueueView raising its Matched event, the real HullbreachQueueConnect.
    public sealed class QueueConnectPlayTests
    {
        const string MatchId = "6f1c1c1e-0a53-4d2e-9d57-3f2a6a8d9b10";
        const string Skin = "hull-skin-deep-sea-camo";

        [TearDown]
        public void TearDown() => HullbreachQueueTicket.Clear();

        [UnityTest]
        public IEnumerator Matched_DialsTheAssignedServer_PresentsTheTicket_AndLoadsTheGameScene()
        {
            // frob:tests Assets/Scripts/Hullbreach.NetCode.Entities/HullbreachQueueConnect.cs::Hullbreach.NetCode.Entities.HullbreachQueueConnect kind="integration"
            var log = new List<string>();
            void Capture(string message, string stack, LogType type) => log.Add(message);
            Application.logMessageReceived += Capture;
            try
            {
                SceneManager.LoadScene("LobbyScene");
                yield return null;
                yield return null;

                var view = UnityEngine.Object.FindFirstObjectByType<HullbreachQueueView>(FindObjectsInactive.Include);
                Assert.IsNotNull(view, "LobbyScene has no HullbreachQueueView");
                Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<HullbreachQueueConnect>(FindObjectsInactive.Include),
                    "nothing attached HullbreachQueueConnect when the lobby loaded");

                // The view declares `public event Action<MatchHandoff> Matched`; raise it the way the machine does.
                FieldInfo backing = typeof(HullbreachQueueView).GetField("Matched",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(backing, "Matched event backing field not found");
                var handler = (Action<MatchHandoff>)backing.GetValue(view);
                Assert.IsNotNull(handler, "nothing subscribed to HullbreachQueueView.Matched");
                handler(new MatchHandoff(MatchId, "127.0.0.1", 7979, "opponent", Skin, null));

                float deadline = Time.realtimeSinceStartup + 10f;
                while (SceneManager.GetActiveScene().name != DirectLaunchPlan.GameScene && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.AreEqual(DirectLaunchPlan.GameScene, SceneManager.GetActiveScene().name,
                    "a found match did not load the game scene");
                Assert.AreEqual(MatchId, HullbreachQueueTicket.Pending, "the match id is not queued for the join ticket");
                Assert.AreEqual(Skin, HullbreachQueueTicket.Skin);
                Assert.IsTrue(log.Exists(m => m.Contains("Hullbreach connect: server=127.0.0.1:7979") && m.Contains(MatchId)),
                    "no 'Hullbreach connect: server=127.0.0.1:7979 match_id=...' dial line was logged");
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }
        }
    }
}
