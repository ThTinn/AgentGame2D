using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonOnline.Characters
{
    /// <summary>
    /// Geometry for trimming LPC sheets: an LPC sheet is a grid of 64 x 64 frames, four rows (up,
    /// left, down, right). The game keeps one row and only the columns its animations use, cropped
    /// to one box shared by every kept frame so the feet pivot stays put. Pure math for the editor
    /// importer, kept here so it is unit-testable.
    /// </summary>
    public static class LpcSheetLayout
    {
        /// <summary>LPC frames are 64 x 64 pixels.</summary>
        public const int FRAME_SIZE = 64;

        /// <summary>Feet sit on the vertical centre line of the frame.</summary>
        public const float FEET_X = FRAME_SIZE / 2f;

        /// <summary>Feet rest on the line 62 px below the frame's top edge.</summary>
        public const float FEET_Y_FROM_TOP = 62f;

        /// <summary>
        /// A crop shared by the kept frames of one sheet. <see cref="X"/> and <see cref="Y"/> are the
        /// box's top-left corner inside a frame, measured from the frame's top-left.
        /// </summary>
        [Serializable]
        public sealed class Crop
        {
            /// <summary>Original sheet columns kept, in strip order.</summary>
            public int[] Columns;
            /// <summary>Left edge of the box inside a frame, in pixels.</summary>
            public int X;
            /// <summary>Top edge of the box inside a frame, in pixels from the top.</summary>
            public int Y;
            /// <summary>Box width in pixels; each frame of the cropped strip is this wide.</summary>
            public int Width;
            /// <summary>Box height in pixels; the cropped strip is this tall.</summary>
            public int Height;
        }

        /// <summary>
        /// The smallest box holding every opaque pixel of the given columns of one row.
        /// A row with no opaque pixel gets a 1 x 1 box so the strip stays valid.
        /// </summary>
        /// <param name="pixels">Sheet pixels, row-major from the bottom-left (Unity's order).</param>
        /// <param name="sheetWidth">Sheet width in pixels.</param>
        /// <param name="sheetHeight">Sheet height in pixels.</param>
        /// <param name="rowFromTop">Frame row to keep, 0 = top row.</param>
        /// <param name="columns">Frame columns to keep.</param>
        public static Crop FindCrop(Color32[] pixels, int sheetWidth, int sheetHeight, int rowFromTop, IReadOnlyList<int> columns)
        {
            if (pixels == null || pixels.Length != sheetWidth * sheetHeight) throw new ArgumentException("pixel count does not match the sheet size", nameof(pixels));
            var rowBottom = sheetHeight - (rowFromTop + 1) * FRAME_SIZE;
            if (rowBottom < 0) throw new ArgumentOutOfRangeException(nameof(rowFromTop));

            int x0 = FRAME_SIZE, x1 = -1, top0 = FRAME_SIZE, top1 = -1;
            foreach (var column in columns)
            {
                if ((column + 1) * FRAME_SIZE > sheetWidth) throw new ArgumentOutOfRangeException(nameof(columns), $"column {column} is outside the sheet");
                for (var fy = 0; fy < FRAME_SIZE; fy++)
                {
                    var rowStart = (rowBottom + fy) * sheetWidth + column * FRAME_SIZE;
                    for (var fx = 0; fx < FRAME_SIZE; fx++)
                    {
                        if (pixels[rowStart + fx].a == 0) continue;
                        var fromTop = FRAME_SIZE - 1 - fy;
                        x0 = Math.Min(x0, fx); x1 = Math.Max(x1, fx);
                        top0 = Math.Min(top0, fromTop); top1 = Math.Max(top1, fromTop);
                    }
                }
            }

            var cols = new int[columns.Count];
            for (var i = 0; i < cols.Length; i++) cols[i] = columns[i];
            if (x1 < 0) return new Crop { Columns = cols, X = 0, Y = 0, Width = 1, Height = 1 };
            return new Crop { Columns = cols, X = x0, Y = top0, Width = x1 - x0 + 1, Height = top1 - top0 + 1 };
        }

        /// <summary>
        /// Copies the kept frames into a strip, left to right in <see cref="Crop.Columns"/> order.
        /// </summary>
        /// <returns>Strip pixels, row-major from the bottom-left, <c>Width * Columns.Length</c> wide.</returns>
        public static Color32[] BuildStrip(Color32[] pixels, int sheetWidth, int sheetHeight, int rowFromTop, Crop crop)
        {
            var rowBottom = sheetHeight - (rowFromTop + 1) * FRAME_SIZE;
            var stripWidth = crop.Width * crop.Columns.Length;
            var strip = new Color32[stripWidth * crop.Height];
            // the box's bottom edge, in frame pixels from the frame's bottom
            var boxBottom = FRAME_SIZE - (crop.Y + crop.Height);
            for (var k = 0; k < crop.Columns.Length; k++)
            {
                var sourceLeft = crop.Columns[k] * FRAME_SIZE + crop.X;
                for (var y = 0; y < crop.Height; y++)
                {
                    var source = (rowBottom + boxBottom + y) * sheetWidth + sourceLeft;
                    Array.Copy(pixels, source, strip, y * stripWidth + k * crop.Width, crop.Width);
                }
            }
            return strip;
        }

        /// <summary>
        /// The feet point as a normalized pivot of a cropped frame. It can fall outside 0..1, e.g. for
        /// a hat whose box ends above the feet.
        /// </summary>
        public static Vector2 FeetPivot(Crop crop) =>
            new Vector2((FEET_X - crop.X) / crop.Width, (crop.Height - (FEET_Y_FROM_TOP - crop.Y)) / crop.Height);
    }
}
