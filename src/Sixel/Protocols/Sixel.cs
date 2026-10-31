using System.Text;
using Sixel.Terminal;

namespace Sixel.Protocols;

public static partial class Sixel {
    // Backends supply RGBA pixels; palette assignment, runs and six-row bands
    // are independent of the image library.
    private sealed class FrameEncoder {
        private readonly StringBuilder data;
        private readonly StringBuilder paletteData;
        private readonly Dictionary<uint, int> palette = new(256);
        private readonly int height;
        private int row;
        private int lastColor = -1;
        private int repeatCount;

        internal FrameEncoder(int width, int height) {
            this.height = height;
            int capacity = width * height / 4;
            data = new StringBuilder(capacity);
            paletteData = new StringBuilder(capacity / 2);
            paletteData.StartSixel(width, height);
        }

        internal void Pixel(byte red, byte green, byte blue, byte alpha) {
            int color = -1;
            if (alpha != 0) {
                uint rgba = (uint)(red | green << 8 | blue << 16 | alpha << 24);
                if (!palette.TryGetValue(rgba, out color)) {
                    color = palette.Count;
                    palette.Add(rgba, color);
                    paletteData.AddColorToPalette(red, green, blue, color);
                }
            }
            if (color == lastColor || repeatCount == 0) {
                lastColor = color;
                repeatCount++;
                return;
            }
            WriteRun();
            lastColor = color;
            repeatCount = 1;
        }

        private void WriteRun() => data.AppendSixel(lastColor, repeatCount, (char)('?' + (1 << (row % 6))));

        internal void EndRow() {
            WriteRun();
            data.AppendCarriageReturn();
            if (row % 6 == 5 && row + 1 < height) data.AppendNextLine();
            row++;
            lastColor = -1;
            repeatCount = 0;
        }

        internal string Finish() {
            data.AppendExitSixel();
#if NET472
            return paletteData.Append(data).ToString();
#else
            return string.Create(checked(paletteData.Length + data.Length), this, static (output, encoder) => {
                int prefix = encoder.paletteData.Length;
                encoder.paletteData.CopyTo(0, output, prefix);
                encoder.data.CopyTo(0, output[prefix..], encoder.data.Length);
            });
#endif
        }
    }
    private static void AddColorToPalette(this StringBuilder sixelBuilder, byte red, byte green, byte blue, int colorIndex) {
        // rgb 0-255 needs to be translated to 0-100 for sixel.
        (int r, int g, int b) = (
            red * 100 / 255,
            green * 100 / 255,
            blue * 100 / 255
        );

        _ = sixelBuilder
        .Append(Constants.SixelColorStart)
        .Append(colorIndex)
        .Append(Constants.SixelColorParam)
        .Append(r)
        .Append(Constants.Divider)
        .Append(g)
        .Append(Constants.Divider)
        .Append(b);
    }
    private static void AppendSixel(this StringBuilder sixelBuilder, int colorIndex, int repeatCounter, char sixel) {
        if (colorIndex < 0) {
            sixel = Constants.SixelTransparent;
        }
        else {
            sixelBuilder
            .Append(Constants.SixelColorStart)
            .Append(colorIndex);
        }
        if (repeatCounter <= 1) {
            // single entry
            _ = sixelBuilder
            .Append(sixel);
        }
        else {
            // add repeats
            _ = sixelBuilder
            .Append(Constants.SixelRepeat)
            .Append(repeatCounter)
            .Append(sixel);
        }
    }
    private static void AppendCarriageReturn(this StringBuilder sixelBuilder) {
        _ = sixelBuilder
        .Append(Constants.SixelDECGCR);
    }

    private static void AppendNextLine(this StringBuilder sixelBuilder) {
        _ = sixelBuilder
        .Append(Constants.SixelDECGNL);
    }

    private static void AppendExitSixel(this StringBuilder sixelBuilder) {
        _ = sixelBuilder
        .Append(Constants.ST);
    }

    private static void StartSixel(this StringBuilder sixelBuilder, int width, int height) {
        _ = sixelBuilder
        .Append(Constants.SixelStart)
        .Append(Constants.SixelRaster)
        .Append(width)
        .Append(Constants.Divider)
        .Append(height)
        .Append(Constants.SixelTransparentColor);
    }
}
