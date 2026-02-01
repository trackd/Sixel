using System;
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
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <param name="protocol">The image protocol being used (affects alignment)</param>
    /// <returns>Image size in terminal character cells.</returns>
    public static ImageSize ConvertToCharacterCells(Image<Rgba32> image)
        => GetCharacterCellSize(image.Width, image.Height);

    /// <summary>
    /// Converts image dimensions from pixels to terminal character cells.
    /// </summary>
    /// <param name="imageStream">The image stream to convert.</param>
    /// <param name="protocol">The image protocol being used (affects alignment)</param>
    /// <returns>Image size in terminal character cells.</returns>
    public static ImageSize ConvertToCharacterCells(Stream imageStream) {
        using var image = Image.Load<Rgba32>(imageStream);
        return GetCharacterCellSize(image.Width, image.Height);
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    /// <param name="pixelWidth"></param>
    /// <param name="pixelHeight"></param>
    /// <param name="protocol">The image protocol being used (affects alignment)</param>
    public static ImageSize GetCharacterCellSize(int pixelWidth, int pixelHeight) {
        CellSize cellSize = Compatibility.GetCellSize();

        int widthCells = Math.Max(1, (int)Math.Ceiling((double)pixelWidth / cellSize.PixelWidth));
        int heightCells = Math.Max(1, (int)Math.Ceiling((double)pixelHeight / cellSize.PixelHeight));

        return new ImageSize(widthCells, heightCells);
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    /// <param name="image"></param>
    /// <param name="protocol">The image protocol being used (affects alignment)</param>
    public static ImageSize GetCharacterCellSize(Image<Rgba32> image)
        => GetCharacterCellSize(image.Width, image.Height);

    /// <summary>
    /// Gets the resized size in terminal character cells for an image, given max width/height constraints.
    /// Maintains aspect ratio and uses pixel-space math to avoid clipping.
    /// </summary>
    /// <param name="pixelWidth"></param>
    /// <param name="pixelHeight"></param>
    /// <param name="maxCellWidth"></param>
    /// <param name="maxCellHeight"></param>
    /// <param name="protocol">The image protocol being used (affects alignment)</param>
    public static ImageSize GetResizedCharacterCellSize(int pixelWidth, int pixelHeight, int maxCellWidth, int maxCellHeight) {
        CellSize cellSize = Compatibility.GetCellSize();

        if (pixelWidth <= 0 || pixelHeight <= 0) {
            return new ImageSize(1, 1);
        }

        // Treat 0 as "no constraint" instead of clamping to the current window size.
        bool constrainW = maxCellWidth > 0;
        bool constrainH = maxCellHeight > 0;

        // Convert constraints to pixel budgets; Infinity for unconstrained.
        double maxPixelsW = constrainW ? (double)maxCellWidth * cellSize.PixelWidth : double.PositiveInfinity;
        double maxPixelsH = constrainH ? (double)maxCellHeight * cellSize.PixelHeight : double.PositiveInfinity;

        // Compute scale in pixel space to preserve aspect ratio
        double scaleW = double.IsInfinity(maxPixelsW) ? double.PositiveInfinity : maxPixelsW / pixelWidth;
        double scaleH = double.IsInfinity(maxPixelsH) ? double.PositiveInfinity : maxPixelsH / pixelHeight;
        double scale = Math.Min(scaleW, scaleH);
        if (double.IsInfinity(scale) || scale <= 0) {
            scale = 1.0; // No constraints provided
        }

        // Scaled pixel size (no intermediate rounding to avoid double-rounding)
        double scaledPixelW = Math.Max(1.0, pixelWidth * scale);
        double scaledPixelH = Math.Max(1.0, pixelHeight * scale);

        // Convert scaled pixels to cells. Use Ceil for width to avoid right-edge clipping.
        int cellW = Math.Max(1, (int)Math.Ceiling(scaledPixelW / cellSize.PixelWidth));
        int cellH = Math.Max(1, (int)Math.Ceiling(scaledPixelH / cellSize.PixelHeight));

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
    /// <param name="image"></param>
    /// <param name="maxCellWidth"></param>
    /// <param name="maxCellHeight"></param>
    /// <param name="protocol">The image protocol being used (affects alignment)</param>
    public static ImageSize GetResizedCharacterCellSize(Image<Rgba32> image, int maxCellWidth, int maxCellHeight)
        => GetResizedCharacterCellSize(image.Width, image.Height, maxCellWidth, maxCellHeight);

    /// <summary>
    /// Gets the constrained terminal image size for the image, applying width/height constraints.
    /// </summary>
    /// <param name="image"></param>
    /// <param name="maxWidth"></param>
    /// <param name="maxHeight"></param>
    public static ImageSize GetTerminalImageSize(Image<Rgba32> image, int maxWidth, int maxHeight)
        => GetResizedCharacterCellSize(image.Width, image.Height, maxWidth, maxHeight);

    /// <summary>
    /// Gets the constrained terminal image size for the image, applying width/height constraints.
    /// </summary>
    /// <param name="imageStream"></param>
    /// <param name="maxWidth"></param>
    /// <param name="maxHeight"></param>
    public static ImageSize GetTerminalImageSize(Stream imageStream, int maxWidth, int maxHeight) {
        using var image = Image.Load<Rgba32>(imageStream);
        return GetResizedCharacterCellSize(image.Width, image.Height, maxWidth, maxHeight);
    }

    /// <summary>
    /// Gets the constrained terminal image size, applying width/height constraints.
    /// </summary>
    /// <param name="pixelWidth"></param>
    /// <param name="pixelHeight"></param>
    /// <param name="maxWidth"></param>
    /// <param name="maxHeight"></param>
    public static ImageSize GetTerminalImageSize(int pixelWidth, int pixelHeight, int maxWidth, int maxHeight)
        => GetResizedCharacterCellSize(pixelWidth, pixelHeight, maxWidth, maxHeight);

    internal static ImageSize GetTerminalImageSize(this Image<Rgba32> image)
        => ConvertToCharacterCells(image);

    /// <summary>
    /// Computes a default terminal image size relative to the current window, using true cell size.
    /// When the console is unavailable or redirected, falls back to the natural image size in cells.
    /// </summary>
    /// <param name="image">Loaded image.</param>
    /// <param name="windowScaleFactor">Proportion of window to target (e.g., 0.6 for 60%).</param>
    public static ImageSize GetDefaultTerminalImageSize(Image<Rgba32> image, double windowScaleFactor = 0.6) {
        ImageSize natural = ConvertToCharacterCells(image);

        // If console isn't interactive, return natural size
        bool hasConsole = !Console.IsOutputRedirected && !Console.IsInputRedirected;
        if (!hasConsole) {
            return natural;
        }
#if NET6_0_OR_GREATER
        if (OperatingSystem.IsMacOS()) {
            // this is an attempt to get better sizing for mac retina displays.. testing..
            // is only used when width is not specified.

            // Determine window target in character cells
            int winCols = Math.Max(1, Console.WindowWidth);
            int winRows = Math.Max(1, Console.WindowHeight);
            int targetCols = Math.Max(1, (int)Math.Round(winCols * windowScaleFactor));
            int targetRows = Math.Max(1, (int)Math.Round(winRows * windowScaleFactor));

            // Upscale to meet window-relative targets when natural is smaller
            int applyW = natural.Width < targetCols ? targetCols : natural.Width;
            int applyH = natural.Height < targetRows ? targetRows : natural.Height;
            return GetResizedCharacterCellSize(image.Width, image.Height, applyW, applyH);
        }
#endif
        return natural;

    }
}
