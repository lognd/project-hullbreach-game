using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Hud;
using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    /// <summary>Covers every public member of <see cref="BuilderHudModel"/>
    /// against a fresh <see cref="BuilderSession"/>, checking the exact text
    /// the old <c>BuilderHud.OnGUI</c> produced (D7: behaviour parity).</summary>
    public sealed class BuilderHudModelTests
    {
        [Test]
        public void Title_IsFixedPaletteHeading()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, string.Empty);
            Assert.AreEqual("Palette (keys 1-7)", model.Title);
        }

        [Test]
        public void Rows_OneEntryPerPaletteItem_SelectedMarkedWithArrow()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, string.Empty);

            int count = 0;
            foreach (var entry in BlockPalette.All()) count++;
            Assert.AreEqual(count, model.Rows.Count);

            // Core is selected by default (BuilderSession.SelectedTypeId starts at BlockTypes.Core).
            var core = BlockPalette.All().GetEnumerator();
            core.MoveNext();
            var expected = $"> {core.Current.Name}  mass {core.Current.Mass:0.0}  cost {core.Current.Cost}";
            Assert.AreEqual(expected, model.Rows[0]);
        }

        [Test]
        public void Rows_UnselectedEntriesUseTwoSpaceMarker()
        {
            var session = new BuilderSession();
            session.Select(BlockTypes.Hull);
            var model = BuilderHudModel.Build(session, string.Empty);

            StringAssert.StartsWith("  Core", model.Rows[0]);
            StringAssert.StartsWith("> Hull", model.Rows[1]);
        }

        [Test]
        public void TotalMassLine_MatchesSessionMass()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, string.Empty);
            Assert.AreEqual($"Total mass: {session.TotalMass:0.0}", model.TotalMassLine);
        }

        [Test]
        public void BlockCountLine_MatchesSessionCount()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, string.Empty);
            Assert.AreEqual($"Block count: {session.BlockCount}", model.BlockCountLine);
        }

        [Test]
        public void StateLine_MatchesSessionState()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, string.Empty);
            Assert.AreEqual($"State: {session.State}", model.StateLine);
        }

        [Test]
        public void HoverLine_NullWhenNoVerdictText()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, string.Empty);
            Assert.IsNull(model.HoverLine);
        }

        [Test]
        public void HoverLine_PrefixedWhenVerdictTextPresent()
        {
            var session = new BuilderSession();
            var model = BuilderHudModel.Build(session, "Occupied");
            Assert.AreEqual("Hover: Occupied", model.HoverLine);
        }
    }
}
