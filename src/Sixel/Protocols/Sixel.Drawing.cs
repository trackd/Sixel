#if NET472
using System.Drawing;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Protocols;

public static partial class Sixel {
    /// <summary>Converts an image to a Sixel string.</summary>
    public static string ImageToSixel(Bitmap image, ImageSize imageSize, int maxColors) {
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height, imageSize.Width, imageSize.Height,
            Compatibility.GetCellSize(), sixel: true);
        using var resized = DrawingRaster.Resize(image, layout.Pixels.Width, layout.Pixels.Height);
        var raster = new DrawingRaster(resized);
        raster.Quantize(maxColors);
        return FrameToSixelString(raster);
    }

    internal static string FrameToSixelString(DrawingRaster raster) {
        var encoder = new FrameEncoder(raster.Width, raster.Height);
        for (int y = 0; y < raster.Height; y++) {
            foreach (int pixel in raster.Pixels.AsSpan(y * raster.Width, raster.Width)) {
                encoder.Pixel((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel, (byte)(pixel >> 24));
            }
            encoder.EndRow();
        }
        return encoder.Finish();
    }
}
#endif
