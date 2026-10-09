using System.Collections.Generic;
using System.Globalization;
using Hullbreach.Builder;
using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    // The Hud models are documented as locale-independent; a comma-decimal
    // thread culture must not change any produced text.
    public sealed class HudCultureTests
    {
        CultureInfo _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        }

        [TearDown]
        public void TearDown() => CultureInfo.CurrentCulture = _saved;

        [Test]
        public void FlightTelemetry_UsesInvariantDecimalPoint()
        {
            var model = FlightTelemetryModel.Build(3f, 4f, -0.25f, 0.455f, 0f, -0.1f);
            Assert.AreEqual("Speed: 5.0   Angular speed: 0.25", model.SpeedLine);
            Assert.AreEqual("Steer   -10%", model.Steer.Label);
        }

        [Test]
        public void HullWarning_UsesInvariantDecimalPoint()
        {
            var model = HullWarningModel.Build(HullWarning.Strain, 0.987f, 0, "", 0f);
            Assert.AreEqual("Hull: STRAIN   (max ratio 0.99)", model.Headline);
        }

        [Test]
        public void BuilderHud_UsesInvariantDecimalPoint()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, string.Empty);
            StringAssert.DoesNotContain(",", model.TotalMassLine);
            StringAssert.Contains(".", model.TotalMassLine);
        }

        [Test]
        public void StatusPanel_UsesInvariantDecimalPoint()
        {
            var model = StatusPanelModel.Build(true, "None", 12.3f, 4, new List<ActivePowerup>());
            Assert.AreEqual("Mass: 12.3   Blocks: 4", model.MassBlocksLine);
        }
    }
}
