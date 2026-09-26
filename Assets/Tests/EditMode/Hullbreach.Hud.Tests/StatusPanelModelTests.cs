using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // Covers every public member of StatusPanelModel/ActivePowerup against
    // the exact text the old DrawStatusPanel/DrawActivePowerups drew (D7).
    public sealed class StatusPanelModelTests
    {
        [Test]
        public void ModeLine_Build()
        {
            var model = StatusPanelModel.Build(true, "None", 0f, 0, null);
            Assert.AreEqual("Mode: Build  (Tab to switch)", model.ModeLine);
        }

        [Test]
        public void ModeLine_Fly()
        {
            var model = StatusPanelModel.Build(false, "None", 0f, 0, null);
            Assert.AreEqual("Mode: Fly  (Tab to switch)", model.ModeLine);
        }

        [Test]
        public void ControlLines_Build_ThreeHintLines()
        {
            var model = StatusPanelModel.Build(true, "None", 0f, 0, null);
            Assert.AreEqual(3, model.ControlLines.Count);
            Assert.AreEqual("Left click: place   Right click: remove", model.ControlLines[0]);
            Assert.AreEqual("Ctrl+Z: undo   Ctrl+Shift+Z: redo   Esc: cancel orientation", model.ControlLines[1]);
            Assert.AreEqual("Keys 1-7: select palette entry", model.ControlLines[2]);
        }

        [Test]
        public void ControlLines_Fly_TwoHintLines()
        {
            var model = StatusPanelModel.Build(false, "None", 0f, 0, null);
            Assert.AreEqual(2, model.ControlLines.Count);
            Assert.AreEqual("W/S or Up/Down: thrust/reverse   A/D or Left/Right: steer", model.ControlLines[0]);
            Assert.AreEqual("Space: fire   O: cycle overlay   R: reset to start", model.ControlLines[1]);
        }

        [Test]
        public void OverlayLine_NullInBuild()
        {
            var model = StatusPanelModel.Build(true, "Stress", 0f, 0, null);
            Assert.IsNull(model.OverlayLine);
        }

        [Test]
        public void OverlayLine_PresentInFly()
        {
            var model = StatusPanelModel.Build(false, "Stress", 0f, 0, null);
            Assert.AreEqual("Overlay: Stress", model.OverlayLine);
        }

        [Test]
        public void MassBlocksLine_FormatsMassAndCount()
        {
            var model = StatusPanelModel.Build(true, "None", 12.5f, 7, null);
            Assert.AreEqual("Mass: 12.5   Blocks: 7", model.MassBlocksLine);
        }

        [Test]
        public void PowerupLines_EmptyWhenNoPowerups()
        {
            var model = StatusPanelModel.Build(false, "None", 0f, 0, null);
            Assert.AreEqual(0, model.PowerupLines.Count);

            var withEmptyList = StatusPanelModel.Build(false, "None", 0f, 0, new List<ActivePowerup>());
            Assert.AreEqual(0, withEmptyList.PowerupLines.Count);
        }

        [Test]
        public void PowerupLines_OneLinePerActivePowerup()
        {
            var powerups = new List<ActivePowerup>
            {
                new ActivePowerup("Cannon", 2, 0, BlockTypes.Cannon, 1, 7.3f),
            };
            var model = StatusPanelModel.Build(false, "None", 0f, 0, powerups);
            Assert.AreEqual(1, model.PowerupLines.Count);
            Assert.AreEqual("Cannon (2,0): Gravity gun 7.3 s", model.PowerupLines[0]);
        }

        [Test]
        public void VariantLabel_KnownVariants()
        {
            Assert.AreEqual("Gravity gun", StatusPanelModel.VariantLabel(BlockTypes.Cannon, 1));
            Assert.AreEqual("Anti-gravity gun", StatusPanelModel.VariantLabel(BlockTypes.Cannon, 2));
            Assert.AreEqual("Seeking thruster", StatusPanelModel.VariantLabel(BlockTypes.Thruster, 1));
        }

        [Test]
        public void VariantLabel_UnknownFallsBackToGenericName()
        {
            Assert.AreEqual("variant 9", StatusPanelModel.VariantLabel(BlockTypes.Hull, 9));
        }
    }
}
