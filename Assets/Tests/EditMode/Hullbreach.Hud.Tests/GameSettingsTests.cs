using System.Collections.Generic;
using System.IO;
using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // S27-1 / S27-3 / S37-2: settings clamp, round-trip through a file and tolerate junk.
    public sealed class GameSettingsTests
    {
        string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "hullbreach-settings-" + Path.GetRandomFileName());

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Volumes_ClampToUnitRange()
        {
            var s = new GameSettings { MasterVolume = 3f, MusicVolume = -1f, EffectsVolume = float.NaN };
            Assert.AreEqual(1f, s.MasterVolume);
            Assert.AreEqual(0f, s.MusicVolume);
            Assert.AreEqual(0f, s.EffectsVolume);
        }

        [Test]
        public void SetResolution_RejectsTinySizes()
        {
            var s = new GameSettings();
            Assert.IsFalse(s.SetResolution(100, 100));
            Assert.AreEqual(1280, s.ResolutionWidth);
            Assert.IsTrue(s.SetResolution(1920, 1080));
            Assert.AreEqual(1080, s.ResolutionHeight);
        }

        [Test]
        public void SerializeParse_RoundTrips()
        {
            var s = new GameSettings { MasterVolume = 0.25f, MusicVolume = 0.5f, EffectsVolume = 0.75f, Fullscreen = true, ColorblindPalette = true };
            s.SetResolution(1600, 900);
            var back = GameSettings.Parse(s.Serialize());
            Assert.AreEqual(0.25f, back.MasterVolume);
            Assert.AreEqual(0.5f, back.MusicVolume);
            Assert.AreEqual(0.75f, back.EffectsVolume);
            Assert.AreEqual(1600, back.ResolutionWidth);
            Assert.AreEqual(900, back.ResolutionHeight);
            Assert.IsTrue(back.Fullscreen);
            Assert.IsTrue(back.ColorblindPalette);
        }

        [Test]
        public void Parse_SkipsJunkAndKeepsDefaults()
        {
            var s = GameSettings.Parse("master=loud\nwidth=10\nheight=10\nbogus\nfuture=1\ncolorblind=1\n");
            Assert.AreEqual(1f, s.MasterVolume);
            Assert.AreEqual(1280, s.ResolutionWidth);
            Assert.IsTrue(s.ColorblindPalette);
            Assert.IsFalse(GameSettings.Parse(null).ColorblindPalette);
        }

        [Test]
        public void Store_PersistsAcrossInstances_AndLogs()
        {
            var lines = new List<string>();
            string path = Path.Combine(_dir, "nested", "settings.txt");
            Assert.IsTrue(new GameSettingsStore(path, lines.Add).Save(new GameSettings { ColorblindPalette = true }));
            Assert.IsTrue(new GameSettingsStore(path, lines.Add).Load().ColorblindPalette);
            Assert.AreEqual(2, lines.Count);
        }

        [Test]
        public void Store_MissingFileGivesDefaults()
        {
            var s = new GameSettingsStore(Path.Combine(_dir, "none.txt")).Load();
            Assert.IsFalse(s.ColorblindPalette);
            Assert.AreEqual(1f, s.MasterVolume);
        }
    }
}
