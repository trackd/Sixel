#if !NET472
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Protocols;

public static partial class Sixel {
    /// <summary>Converts an image to a Sixel string.</summary>
    public static string ImageToSixel(Image<Rgba32> image, ImageSize imageSize, int maxColors) {
        Image<Rgba32> resized = Resizer.ResizeToCharacterCells(image, imageSize, maxColors);
        return FrameToSixelString(resized.Frames[0]);
    }

    internal static string FrameToSixelString(ImageFrame<Rgba32> frame) {
        var encoder = new FrameEncoder(frame.Width, frame.Height);
        frame.ProcessPixelRows(accessor => {
            for (int y = 0; y < accessor.Height; y++) {
                foreach (Rgba32 pixel in accessor.GetRowSpan(y))
                    encoder.Pixel(pixel.R, pixel.G, pixel.B, pixel.A);
                encoder.EndRow();
            }
        });
        return encoder.Finish();
    }
}
#endif
