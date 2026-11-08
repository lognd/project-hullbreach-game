using System.IO;
using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // S27-1 / S37-2: the settings screen edits GameSettings and saves on Apply.
    // frob:ticket 01M3DG5Y1GSP9K4A0MY9MSHS37
    // frob:ticket 01M3DG5Y2DX8GJYANMCGM0B19P
    public sealed class SettingsScreenModelTests
    {
        string _dir;
        string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "hullbreach-screen-" + Path.GetRandomFileName());
            _path = Path.Combine(_dir, "settings.txt");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        SettingsScreenModel Make() => new SettingsScreenModel(new GameSettings(), new GameSettingsStore(_path));

        // frob:tests SettingsScreenModel.SetMaster
        // frob:tests SettingsScreenModel.PercentLabel
        [Test]
        public void VolumeSettersClampAndLabelAsPercent()
        {
            SettingsScreenModel m = Make();
            m.SetMaster(2f);
            m.SetMusic(0.5f);
            m.SetEffects(-1f);
            Assert.AreEqual(1f, m.Settings.MasterVolume);
            Assert.AreEqual("100%", SettingsScreenModel.PercentLabel(m.Settings.MasterVolume));
            Assert.AreEqual("50%", SettingsScreenModel.PercentLabel(m.Settings.MusicVolume));
            Assert.AreEqual("0%", SettingsScreenModel.PercentLabel(m.Settings.EffectsVolume));
        }

        // frob:tests SettingsScreenModel.StepResolution
        [Test]
        public void StepResolutionWrapsBothWays()
        {
            SettingsScreenModel m = Make();
            Assert.AreEqual(0, m.ResolutionIndex);
            m.StepResolution(-1);
            Assert.AreEqual(SettingsScreenModel.Resolutions.Length - 1, m.ResolutionIndex);
            m.StepResolution(1);
            Assert.AreEqual(0, m.ResolutionIndex);
            m.StepResolution(1);
            Assert.AreEqual("1600 x 900", m.ResolutionLabel);
        }

        // frob:tests SettingsScreenModel.StepResolution
        [Test]
        public void CustomResolutionStepsToAnEnd()
        {
            SettingsScreenModel m = Make();
            m.Settings.SetResolution(1000, 700);
            Assert.AreEqual(-1, m.ResolutionIndex);
            m.StepResolution(1);
            Assert.AreEqual(0, m.ResolutionIndex);
        }

        // frob:tests SettingsScreenModel.SetColorblind
        // frob:tests SettingsScreenModel.Apply
        [Test]
        public void ColorblindToggleSurvivesApplyAndReload()
        {
            SettingsScreenModel m = Make();
            m.SetColorblind(true);
            m.SetFullscreen(true);
            Assert.IsTrue(m.Apply());
            Assert.AreEqual("Settings saved.", m.Message);
            GameSettings loaded = new GameSettingsStore(_path).Load();
            Assert.IsTrue(loaded.ColorblindPalette);
            Assert.IsTrue(loaded.Fullscreen);
        }

        // frob:tests SettingsScreenModel.Apply
        [Test]
        public void ApplyReportsAFailedWrite()
        {
            Directory.CreateDirectory(_dir);
            // A directory where the file should be makes the write fail.
            Directory.CreateDirectory(_path);
            SettingsScreenModel m = Make();
            Assert.IsFalse(m.Apply());
            Assert.IsTrue(m.IsError);
        }

        // frob:tests SettingsScreenModel.SetMusic
        [Test]
        public void EditsRaiseChangedAndClearTheMessage()
        {
            SettingsScreenModel m = Make();
            int raised = 0;
            m.Changed += () => raised++;
            m.Apply();
            m.SetMusic(0.3f);
            Assert.AreEqual(2, raised);
            Assert.AreEqual(string.Empty, m.Message);
        }
    }
}
