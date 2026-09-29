using Sixel.Terminal.Models;
using SixLabors.ImageSharp;

namespace Sixel.Terminal;

/// <summary>Resolves pixels and occupied cells together, before any rasterization.</summary>
internal readonly struct ImageLayout {
    internal Size Pixels { get; }
    internal ImageSize Cells { get; }

    private ImageLayout(Size pixels, ImageSize cells) {
        Pixels = pixels;
        Cells = cells;
    }

    internal static ImageSize Measure(int width, int height, CellSize cell, bool sixel) {
        ValidateCell(cell);
        long occupiedHeight = sixel ? ((long)height + 5) / 6 * 6 : height;
        return new ImageSize(
            checked((int)Math.Max(1, ((long)width + cell.PixelWidth - 1) / cell.PixelWidth)),
            checked((int)Math.Max(1, (occupiedHeight + cell.PixelHeight - 1) / cell.PixelHeight)));
    }

    internal static ImageLayout Calculate(int width, int height, int columns, int rows, CellSize cell, bool sixel) {
        ValidateCell(cell);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");

        double maxWidth = columns > 0 ? checked(columns * cell.PixelWidth) : double.PositiveInfinity;
        double maxHeight = rows > 0 ? checked(rows * cell.PixelHeight) : double.PositiveInfinity;
        if (sixel && rows > 0) {
            maxHeight = Math.Floor(maxHeight / 6) * 6;
            if (maxHeight < 6)
                throw new ArgumentOutOfRangeException(nameof(rows), "The height must accommodate at least one sixel band (6 pixels).");
        }

        double scale = Math.Min(maxWidth / width, maxHeight / height);
        if (double.IsPositiveInfinity(scale))
            scale = 1;

        // Round once in pixel space. Never turn a rounded cell count back into
        // a larger sixel raster, which can exceed the requested row budget.
        int pixelWidth = Math.Max(1, checked((int)Math.Floor(width * scale)));
        int pixelHeight = Math.Max(1, checked((int)Math.Floor(height * scale)));
        var pixels = new Size(pixelWidth, pixelHeight);
        return new ImageLayout(pixels, Measure(pixelWidth, pixelHeight, cell, sixel));
    }

    private static void ValidateCell(CellSize cell) {
        if (cell.PixelWidth <= 0 || cell.PixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(cell), "Cell dimensions must be positive.");
    }
}
