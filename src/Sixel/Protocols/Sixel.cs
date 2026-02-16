using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Protocols;

public static class Sixel {
    /// <summary>
    /// Converts an image to a Sixel string.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <param name="imageSize">The size of the image in character cells.</param>
    /// <param name="maxColors">The Max colors of the image.</param>
    public static string ImageToSixel(Image<Rgba32> image, ImageSize imageSize, int maxColors) {
        // Use Resizer to handle resizing and quantization
        Image<Rgba32> resizedImage = Resizer.ResizeToCharacterCells(image, imageSize, maxColors);
        ImageFrame<Rgba32> targetFrame = resizedImage.Frames[0];
        return FrameToSixelString(targetFrame);
    }
    internal static string FrameToSixelString(ImageFrame<Rgba32> frame) {
        // Pre-allocate StringBuilder with estimated capacity for better performance
        int estimatedSize = frame.Width * frame.Height / 4; // Rough estimate based on compression
        var sixelBuilder = new StringBuilder(estimatedSize);
        var sixel = new StringBuilder(estimatedSize / 2);
        var palette = new Dictionary<Rgba32, int>(256); // Pre-size for typical max colors
        int colorCounter = 1;
        sixel.StartSixel(frame.Width, frame.Height);
        frame.ProcessPixelRows(accessor => {
            for (int y = 0; y < accessor.Height; y++) {
                Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                // The value of 1 left-shifted by the remainder of the current row divided by 6 gives the correct sixel character offset from the empty sixel char for each row.
                // See the description of s...s for more detail on the sixel format https://vt100.net/docs/vt3xx-gp/chapter14.html#S14.2.1

                // modulus trick from https://github.com/sxyazi/yazi/blob/main/yazi-adapter/src/sixel.rs (MIT)
                char c = (char)('?' + (1 << (y % 6)));
                int lastColor = -1;
                int repeatCounter = 0;
                foreach (ref Rgba32 pixel in pixelRow) {
                    if (!palette.TryGetValue(pixel, out int colorIndex)) {
                        // The colors can be added to the palette and interleaved with the sixel data so long as the color is defined before it is used.
                        // for compatibility testing im not doing this at the moment.
                        colorIndex = colorCounter++;
                        palette[pixel] = colorIndex;
                        sixel.AddColorToPalette(pixel, colorIndex);
                    }

                    // Transparency is a special color index of 0 that exists in our sixel palette.
                    int colorId = pixel.A == 0 ? 0 : colorIndex;

                    // Sixel data will use a repeat entry if the color is the same as the last one.
                    // https://vt100.net/docs/vt3xx-gp/chapter14.html#S14.3.1
                    if (colorId == lastColor || repeatCounter == 0) {
                        // If the color was repeated go to the next loop iteration to check the next pixel.
                        lastColor = colorId;
                        repeatCounter++;
                        continue;
                    }

                    // Every time the color is not repeated the previous color is written to the string.
                    sixelBuilder.AppendSixel(lastColor, repeatCounter, c);

                    // Remember the current color and reset the repeat counter.
                    lastColor = colorId;
                    repeatCounter = 1;
                }

                // Write the last color and repeat counter to the string for the current row.
                sixelBuilder.AppendSixel(lastColor, repeatCounter, c);

                // Add a carriage return at the end of each row and a new line every 6 pixel rows.
                sixelBuilder.AppendCarriageReturn();
                if (y % 6 == 5) {
                    sixelBuilder.AppendNextLine();
                }
            }
        });
        sixelBuilder.AppendNextLine();
        sixelBuilder.AppendExitSixel();

        return sixel.Append(sixelBuilder).ToString();
    }

    private static void AddColorToPalette(this StringBuilder sixelBuilder, Rgba32 pixel, int colorIndex) {
        // rgb 0-255 needs to be translated to 0-100 for sixel.
        (int r, int g, int b) = (
            pixel.R * 100 / 255,
            pixel.G * 100 / 255,
            pixel.B * 100 / 255
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
        if (colorIndex == 0) {
            // Transparent pixels are a special case and are always 0 in the palette.
            sixel = Constants.SixelTransparent;
        }
        if (repeatCounter <= 1) {
            // single entry
            _ = sixelBuilder
            .Append(Constants.SixelColorStart)
            .Append(colorIndex)
            .Append(sixel);
        }
        else {
            // add repeats
            _ = sixelBuilder
            .Append(Constants.SixelColorStart)
            .Append(colorIndex)
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
