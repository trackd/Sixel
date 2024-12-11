using Sixel.Terminal.Models;
using System.Collections;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Terminal;

public static class SizeHelper {
    public static Size GetTerminalImageSize(int Width, int Height, int? maxWidth = null)
    {
        var cellSize = Compatibility.GetCellSize();

        // calculate natural dimensions in cells (rounded down to ensure fit)
        var naturalCellWidth = (int)Math.Floor((double)Width / cellSize.PixelWidth);
        var naturalCellHeight = (int)Math.Floor((double)Height / cellSize.PixelHeight);

        // adjust width if needed by maxWidth
        var targetCellWidth = maxWidth.HasValue && maxWidth.Value > 0
            ? Math.Min(naturalCellWidth, maxWidth.Value)
            : Math.Min(naturalCellWidth, Console.WindowWidth - 2);

        if (targetCellWidth == naturalCellWidth)
        {
            // No resize needed
            return new Size(naturalCellWidth, naturalCellHeight);
        }

        // Calculate new height maintaining aspect ratio
        var targetPixelWidth = targetCellWidth * cellSize.PixelWidth;
        var targetPixelHeight = (int)Math.Round((double)Height / Width * targetPixelWidth);

        // round down height to nearest Sixel boundary (6 pixels)
        targetPixelHeight -= targetPixelHeight % 6;

        var targetCellHeight = (int)Math.Floor((double)targetPixelHeight / cellSize.PixelHeight);

        return new Size(targetCellWidth, targetCellHeight);
    }

    public static Size GetTerminalImageSize(Image<Rgba32> image, int? maxWidth = null)
    {
        return GetTerminalImageSize(image.Width, image.Height, maxWidth);
    }
}
