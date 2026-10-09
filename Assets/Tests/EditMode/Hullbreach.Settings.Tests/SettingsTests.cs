using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Hullbreach.Settings;

namespace Hullbreach.Settings.Tests
{
    public class SettingsTests
    {
        // In-memory storage so store logic is tested without a disk.
        sealed class MemoryStorage : ISettingsStorage
        {
            public string Text;
            public ReadOutcome Outcome = ReadOutcome.Missing;
            public bool FailWrites;

            public ReadOutcome Read(out string text)
            {
                text = Outcome == ReadOutcome.Found ? Text : null;
                return Outcome;
            }

            public bool TryWrite(string text, out string error)
            {
                if (FailWrites) { error = "disk full"; return false; }
                Text = text;
                Outcome = ReadOutcome.Found;
                error = null;
                return true;
            }
        }

        static GameSettings Custom()
        {
            var s = GameSettings.Defaults();
            s.MasterVolume = 0.35f;
            s.MusicVolume = 0f;
            s.EffectsVolume = 0.9f;
            s.ResolutionWidth = 1280;
            s.ResolutionHeight = 720;
            s.Fullscreen = false;
            s.KeyBindings["fire"] = "Space";
            s.KeyBindings["thrust"] = "W";
            return s;
        }

        static void AssertSame(GameSettings a, GameSettings b)
        {
            Assert.AreEqual(a.MasterVolume, b.MasterVolume);
            Assert.AreEqual(a.MusicVolume, b.MusicVolume);
            Assert.AreEqual(a.EffectsVolume, b.EffectsVolume);
            Assert.AreEqual(a.ResolutionWidth, b.ResolutionWidth);
            Assert.AreEqual(a.ResolutionHeight, b.ResolutionHeight);
            Assert.AreEqual(a.Fullscreen, b.Fullscreen);
            CollectionAssert.AreEqual(a.KeyBindings, b.KeyBindings);
        }

        [Test]
        public void SaveThenLoad_RoundTripsEveryField()
        {
            var storage = new MemoryStorage();
            var store = new SettingsStore(storage);
            Assert.IsTrue(store.Save(Custom()).Ok);

            var load = store.Load();
            Assert.AreEqual(SettingsLoadStatus.Loaded, load.Status);
            Assert.IsEmpty(load.Warnings);
            AssertSame(Custom(), load.Settings);
        }

        [Test]
        public void MissingFile_GivesDefaults_NotAnError()
        {
            var load = new SettingsStore(new MemoryStorage()).Load();
            Assert.AreEqual(SettingsLoadStatus.Missing, load.Status);
            AssertSame(GameSettings.Defaults(), load.Settings);
        }

        [Test]
        public void UnreadableFile_GivesDefaultsAndCorrupt()
        {
            var load = new SettingsStore(new MemoryStorage { Outcome = ReadOutcome.Unreadable }).Load();
            Assert.AreEqual(SettingsLoadStatus.Corrupt, load.Status);
            AssertSame(GameSettings.Defaults(), load.Settings);
        }

        [TestCase("")]
        [TestCase("   \n")]
        [TestCase("garbage")]
        [TestCase("hullbreach-settings x\n")]
        [TestCase("hullbreach-settings 0\n")]
        [TestCase("\0\0\0\u0001\n")]
        public void CorruptFile_GivesDefaultsAndStatusCorrupt(string text)
        {
            var load = SettingsFile.Parse(text);
            Assert.AreEqual(SettingsLoadStatus.Corrupt, load.Status);
            Assert.IsNotEmpty(load.Warnings);
            AssertSame(GameSettings.Defaults(), load.Settings);
        }

        [Test]
        public void BadValues_KeepThatFieldsDefault_AndWarn()
        {
            var load = SettingsFile.Parse("hullbreach-settings 1\nmaster_volume loud\nmusic_volume 0.25\nresolution_width 12.5\nfullscreen maybe\nbogus 1\n");
            Assert.AreEqual(SettingsLoadStatus.Loaded, load.Status);
            Assert.AreEqual(GameSettings.DefaultMasterVolume, load.Settings.MasterVolume);
            Assert.AreEqual(0.25f, load.Settings.MusicVolume);
            Assert.AreEqual(GameSettings.DefaultWidth, load.Settings.ResolutionWidth);
            Assert.IsTrue(load.Settings.Fullscreen);
            Assert.AreEqual(4, load.Warnings.Count);
        }

        [Test]
        public void OutOfRangeValues_AreClamped()
        {
            var load = SettingsFile.Parse("hullbreach-settings 1\nmaster_volume 7\nmusic_volume -2\nresolution_width 5\nresolution_height 720\n");
            Assert.AreEqual(1f, load.Settings.MasterVolume);
            Assert.AreEqual(0f, load.Settings.MusicVolume);
            Assert.AreEqual(GameSettings.DefaultWidth, load.Settings.ResolutionWidth);
            Assert.AreEqual(GameSettings.DefaultHeight, load.Settings.ResolutionHeight, "an invalid resolution pair resets both edges");
        }

        [Test]
        public void NewerVersion_ParsesKnownKeys_WithWarning()
        {
            var load = SettingsFile.Parse("hullbreach-settings 9\nmaster_volume 0.5\nfuture_thing 1\n");
            Assert.AreEqual(SettingsLoadStatus.Loaded, load.Status);
            Assert.AreEqual(0.5f, load.Settings.MasterVolume);
            Assert.GreaterOrEqual(load.Warnings.Count, 1);
        }

        [Test]
        public void Write_IsStableAndInvariantCulture()
        {
            var prior = System.Globalization.CultureInfo.CurrentCulture;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            try
            {
                Assert.AreEqual(
                    "hullbreach-settings 1\nmaster_volume 0.35\nmusic_volume 0\neffects_volume 0.9\nresolution_width 1280\nresolution_height 720\nfullscreen false\nbind.fire Space\nbind.thrust W\n",
                    SettingsFile.Write(Custom()));
                AssertSame(Custom(), SettingsFile.Parse(SettingsFile.Write(Custom())).Settings);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = prior;
            }
        }

        [Test]
        public void Save_NormalizesWithoutMutatingCaller_AndReportsWriteFailure()
        {
            var storage = new MemoryStorage();
            var store = new SettingsStore(storage);
            var wild = GameSettings.Defaults();
            wild.MasterVolume = 3f;
            Assert.IsTrue(store.Save(wild).Ok);
            Assert.AreEqual(3f, wild.MasterVolume, "caller's object is untouched");
            Assert.AreEqual(1f, store.Load().Settings.MasterVolume);

            storage.FailWrites = true;
            var failed = store.Save(wild);
            Assert.IsFalse(failed.Ok);
            Assert.AreEqual("disk full", failed.Error);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var a = Custom();
            var b = a.Clone();
            b.MasterVolume = 0f;
            b.KeyBindings["fire"] = "X";
            Assert.AreEqual(0.35f, a.MasterVolume);
            Assert.AreEqual("Space", a.KeyBindings["fire"]);
        }

        [Test]
        public void FileStorage_RoundTripsThroughARealFile_AndCreatesDirectories()
        {
            string dir = Path.Combine(Path.GetTempPath(), "hullbreach-settings-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "nested", "settings.txt");
            try
            {
                var store = new SettingsStore(new FileSettingsStorage(path));
                Assert.AreEqual(SettingsLoadStatus.Missing, store.Load().Status);
                Assert.IsTrue(store.Save(Custom()).Ok);
                AssertSame(Custom(), new SettingsStore(new FileSettingsStorage(path)).Load().Settings);
                Assert.IsFalse(File.Exists(path + ".tmp"), "temp file is cleaned up");

                File.WriteAllText(path, "not settings");
                Assert.AreEqual(SettingsLoadStatus.Corrupt, store.Load().Status);
                Assert.IsTrue(store.Save(Custom()).Ok, "a corrupt file is replaced by the next save");
                Assert.AreEqual(SettingsLoadStatus.Loaded, store.Load().Status);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void FileStorage_WriteToUnwritablePath_ReturnsFalseNotException()
        {
            string blocker = Path.Combine(Path.GetTempPath(), "hullbreach-blocker-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "x");
            try
            {
                // A file where a directory is needed makes CreateDirectory fail.
                var storage = new FileSettingsStorage(Path.Combine(blocker, "settings.txt"));
                Assert.IsFalse(storage.TryWrite("x", out string error));
                Assert.IsNotEmpty(error);
            }
            finally
            {
                File.Delete(blocker);
            }
        }
    }
}
