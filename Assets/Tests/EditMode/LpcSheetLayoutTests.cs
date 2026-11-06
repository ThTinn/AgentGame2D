using CoinHop.Characters;
using NUnit.Framework;
using UnityEngine;

namespace CoinHop.Tests
{
    public class LpcSheetLayoutTests
    {
        // a 3-column, 4-row LPC sheet; tests paint pixels into the right-facing row (row 3 from top)
        private const int Columns = 3;
        private const int Rows = 4;
        private const int Size = LpcSheetLayout.FRAME_SIZE;
        private const int Width = Columns * Size;
        private const int Height = Rows * Size;
        private const int RightRow = 3;

        private static readonly Color32 Opaque = new Color32(255, 0, 0, 255);

        [Test]
        public void FindCrop_PixelsInTwoColumns_ReturnsSharedBox()
        {
            // Arrange: column 0 has a pixel at (10, 20), column 2 one at (30, 50), frame coords from the top-left
            var pixels = EmptySheet();
            Paint(pixels, column: 0, x: 10, yFromTop: 20);
            Paint(pixels, column: 2, x: 30, yFromTop: 50);

            // Act
            var crop = LpcSheetLayout.FindCrop(pixels, Width, Height, RightRow, new[] { 0, 2 });

            // Assert
            Assert.AreEqual(10, crop.X);
            Assert.AreEqual(20, crop.Y);
            Assert.AreEqual(21, crop.Width);
            Assert.AreEqual(31, crop.Height);
            CollectionAssert.AreEqual(new[] { 0, 2 }, crop.Columns);
        }

        [Test]
        public void FindCrop_PixelInUnusedColumn_IsIgnored()
        {
            // Arrange
            var pixels = EmptySheet();
            Paint(pixels, column: 0, x: 5, yFromTop: 5);
            Paint(pixels, column: 1, x: 60, yFromTop: 60);

            // Act
            var crop = LpcSheetLayout.FindCrop(pixels, Width, Height, RightRow, new[] { 0 });

            // Assert
            Assert.AreEqual((5, 5, 1, 1), (crop.X, crop.Y, crop.Width, crop.Height));
        }

        [Test]
        public void FindCrop_EmptyRow_ReturnsOnePixelBox()
        {
            // Act
            var crop = LpcSheetLayout.FindCrop(EmptySheet(), Width, Height, RightRow, new[] { 0, 1 });

            // Assert
            Assert.AreEqual((1, 1), (crop.Width, crop.Height));
        }

        [Test]
        public void BuildStrip_TwoColumns_PlacesFramesSideBySide()
        {
            // Arrange: a 2 x 1 box; column 0 holds its pixel on the left, column 2 on the right
            var pixels = EmptySheet();
            Paint(pixels, column: 0, x: 10, yFromTop: 20);
            Paint(pixels, column: 2, x: 11, yFromTop: 20);
            var crop = LpcSheetLayout.FindCrop(pixels, Width, Height, RightRow, new[] { 0, 2 });

            // Act
            var strip = LpcSheetLayout.BuildStrip(pixels, Width, Height, RightRow, crop);

            // Assert: strip is 4 x 1; frame 0 is pixels 0..1, frame 1 is pixels 2..3
            Assert.AreEqual(4, strip.Length);
            Assert.AreEqual(255, strip[0].a);
            Assert.AreEqual(0, strip[1].a);
            Assert.AreEqual(0, strip[2].a);
            Assert.AreEqual(255, strip[3].a);
        }

        [Test]
        public void FeetPivot_UncroppedFrame_MatchesFullFramePivot()
        {
            // Arrange
            var crop = new LpcSheetLayout.Crop { Columns = new[] { 0 }, X = 0, Y = 0, Width = Size, Height = Size };

            // Act
            var pivot = LpcSheetLayout.FeetPivot(crop);

            // Assert: centre line, 2 px above the frame's bottom edge
            Assert.AreEqual(0.5f, pivot.x, 1e-5f);
            Assert.AreEqual(2f / Size, pivot.y, 1e-5f);
        }

        [Test]
        public void FeetPivot_BoxAboveFeet_FallsBelowSprite()
        {
            // Arrange: a hat-like box from y 11 to 29, well above the feet line at 62
            var crop = new LpcSheetLayout.Crop { Columns = new[] { 0 }, X = 10, Y = 11, Width = 36, Height = 19 };

            // Act
            var pivot = LpcSheetLayout.FeetPivot(crop);

            // Assert: the feet stay at the same world point, so the pivot is below the sprite (y < 0)
            Assert.AreEqual((32f - 10f) / 36f, pivot.x, 1e-5f);
            Assert.AreEqual((19f - (62f - 11f)) / 19f, pivot.y, 1e-5f);
            Assert.Less(pivot.y, 0f);
        }

        private static Color32[] EmptySheet() => new Color32[Width * Height];

        private static void Paint(Color32[] pixels, int column, int x, int yFromTop)
        {
            var rowBottom = Height - (RightRow + 1) * Size;
            var y = rowBottom + (Size - 1 - yFromTop);
            pixels[y * Width + column * Size + x] = Opaque;
        }
    }
}
