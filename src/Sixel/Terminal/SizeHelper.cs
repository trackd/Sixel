using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Terminal;

/// <summary>
/// Provides methods for converting and resizing image dimensions to terminal character cell sizes.
/// </summary>
public static class SizeHelper {
    /// <summary>
    /// Converts image dimensions from pixels to terminal character cells.
    /// Accounts for sixel 6px row packing when computing the number of rows occupied.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <returns>Image size in terminal character cells.</returns>
    public static ImageSize ConvertToCharacterCells(Image<Rgba32> image) => GetCharacterCellSize(image.Width, image.Height);

    /// <summary>
    /// Converts image dimensions from pixels to terminal character cells.
    /// Accounts for sixel 6px row packing when computing the number of rows occupied.
    /// </summary>
    /// <param name="imageStream">The image stream to convert.</param>
    /// <returns>Image size in terminal character cells.</returns>
    public static ImageSize ConvertToCharacterCells(Stream imageStream) {
        using var image = Image.Load<Rgba32>(imageStream);
        return GetCharacterCellSize(image.Width, image.Height);
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// Height is computed from the image height rounded up to the nearest multiple of 6 pixels to
    /// match sixel 6px row packing so the number of occupied rows is correct.
    /// </summary>
    public static ImageSize GetCharacterCellSize(int pixelWidth, int pixelHeight) {
        CellSize cellSize = Compatibility.GetEffectiveCellSize();

        // Align image height to a multiple of 6px before converting to rows
        // rows = ceil( ceil(h_px / 6) * 6 / cellHeight_px )
        int effectivePixelHeight = (pixelHeight + 5) / 6 * 6;

        int widthCells = Math.Max(1, (int)Math.Ceiling((double)pixelWidth / cellSize.PixelWidth));
        int heightCells = Math.Max(1, (int)Math.Ceiling((double)effectivePixelHeight / cellSize.PixelHeight));

        return new ImageSize(widthCells, heightCells);
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    public static ImageSize GetCharacterCellSize(Image<Rgba32> image)
        => GetCharacterCellSize(image.Width, image.Height);

    /// <summary>
    /// Gets the resized size in terminal character cells for an image, given max width/height constraints.
    /// Maintains aspect ratio, aligns height to sixel 6px rows, and uses pixel-space math to avoid clipping.
    /// </summary>
    public static ImageSize GetResizedCharacterCellSize(int pixelWidth, int pixelHeight, int maxCellWidth, int maxCellHeight) {
        CellSize cellSize = Compatibility.GetEffectiveCellSize();

        if (pixelWidth <= 0 || pixelHeight <= 0) {
            return new ImageSize(1, 1);
        }

        // Treat 0 as "no constraint" instead of clamping to the current window size.
        bool constrainW = maxCellWidth > 0;
        bool constrainH = maxCellHeight > 0;

        // Convert constraints to pixel budgets; Infinity for unconstrained.
        double maxPixelsW = constrainW ? (double)maxCellWidth * cellSize.PixelWidth : double.PositiveInfinity;
        double maxPixelsH = constrainH ? (double)maxCellHeight * cellSize.PixelHeight : double.PositiveInfinity;

        // Respect sixel: when height is constrained, align the pixel budget to a multiple of 6px.
        if (constrainH) {
            maxPixelsH = Math.Max(6.0, Math.Floor(maxPixelsH / 6.0) * 6.0);
        }

        // Compute scale in pixel space to preserve aspect ratio
        double scaleW = double.IsInfinity(maxPixelsW) ? double.PositiveInfinity : maxPixelsW / pixelWidth;
        double scaleH = double.IsInfinity(maxPixelsH) ? double.PositiveInfinity : maxPixelsH / pixelHeight;
        double scale = Math.Min(scaleW, scaleH);
        if (double.IsInfinity(scale) || scale <= 0) {
            scale = 1.0; // No constraints provided
        }

        // Scaled pixel size
        int scaledPixelW = Math.Max(1, (int)Math.Round(pixelWidth * scale));
        int scaledPixelH = Math.Max(1, (int)Math.Round(pixelHeight * scale));

        // Sixel consumes rows in 6px bands; account for that when converting to terminal rows
        int effectiveScaledPixelH = (scaledPixelH + 5) / 6 * 6;

        // Convert scaled pixels to cells. Use Ceil for width to avoid right-edge clipping.
        int cellW = Math.Max(1, (int)Math.Ceiling((double)scaledPixelW / cellSize.PixelWidth));
        int cellH = Math.Max(1, (int)Math.Ceiling((double)effectiveScaledPixelH / cellSize.PixelHeight));

        // Clamp to explicit constraints only
        if (constrainW) {
            cellW = Math.Min(cellW, maxCellWidth);
        }

        if (constrainH) {
            cellH = Math.Min(cellH, maxCellHeight);
        }

        return new ImageSize(cellW, cellH);
    }

    /// <summary>
    /// Gets the resized size in terminal character cells for an image, given max width/height constraints.
    /// Maintains aspect ratio and ensures proper sixel alignment (multiples of 6 pixels).
    /// </summary>
    public static ImageSize GetResizedCharacterCellSize(Image<Rgba32> image, int maxCellWidth, int maxCellHeight)
        => GetResizedCharacterCellSize(image.Width, image.Height, maxCellWidth, maxCellHeight);

    /// <summary>
    /// Gets the constrained terminal image size for the image, applying width/height constraints.
    /// </summary>
    public static ImageSize GetTerminalImageSize(Image<Rgba32> image, int maxWidth, int maxHeight) => GetResizedCharacterCellSize(image.Width, image.Height, maxWidth, maxHeight);

    /// <summary>
    /// Gets the constrained terminal image size for the image, applying width/height constraints.
    /// </summary>
    public static ImageSize GetTerminalImageSize(Stream imageStream, int maxWidth, int maxHeight) {
        using var image = Image.Load<Rgba32>(imageStream);
        return GetResizedCharacterCellSize(image.Width, image.Height, maxWidth, maxHeight);
    }

    /// <summary>
    /// Gets the constrained terminal image size, applying width/height constraints.
    /// </summary>
    public static ImageSize GetTerminalImageSize(int pixelWidth, int pixelHeight, int maxWidth, int maxHeight) => GetResizedCharacterCellSize(pixelWidth, pixelHeight, maxWidth, maxHeight);

    internal static ImageSize GetTerminalImageSize(this Image<Rgba32> image) => ConvertToCharacterCells(image);
}
