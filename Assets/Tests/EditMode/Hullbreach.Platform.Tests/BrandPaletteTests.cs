using NUnit.Framework;

namespace Hullbreach.Platform.Tests
{
    // The palette matches the platform web exactly.
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    // frob:tests Hullbreach.Platform.BrandPalette
    public sealed class BrandPaletteTests
    {
        [Test]
        public void ConstantsMatchTheWebBrand()
        {
            Assert.AreEqual(0x0B1620u, BrandPalette.Background);
            Assert.AreEqual(0x0F1D2Au, BrandPalette.Panel);
            Assert.AreEqual(0xD6E4ECu, BrandPalette.Ink);
            Assert.AreEqual(0x7F96A6u, BrandPalette.Muted);
            Assert.AreEqual(0x5CC8E8u, BrandPalette.Cyan);
            Assert.AreEqual(0xFF6A1Fu, BrandPalette.Orange);
            Assert.AreEqual("Build. Breach. Claim the void.", BrandPalette.Tagline);
        }

        [Test]
        public void ChannelsUnpack()
        {
            Assert.AreEqual(0xFF, BrandPalette.R(BrandPalette.Orange));
            Assert.AreEqual(0x6A, BrandPalette.G(BrandPalette.Orange));
            Assert.AreEqual(0x1F, BrandPalette.B(BrandPalette.Orange));
        }
    }
}
