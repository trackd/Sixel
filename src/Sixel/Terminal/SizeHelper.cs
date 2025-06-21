using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Terminal;

/// <summary>
/// Provides methods for converting and resizing image dimensions to terminal character cell sizes.
/// </summary>
public static class SizeHelper
{
    /// <summary>
    /// Converts image dimensions from pixels to terminal character cells.
    /// Does not apply any constraints, just converts the natural image size.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <returns>Image size in terminal character cells.</returns>
    public static ImageSize ConvertToCharacterCells(Image<Rgba32> image)
    {
        return ConvertToCharacterCells(image.Width, image.Height);
    }

    /// <summary>
    /// Converts image dimensions from pixels to terminal character cells.
    /// Does not apply any constraints, just converts the natural image size.
    /// </summary>
    /// <param name="imageStream">The image stream to convert.</param>
    /// <returns>Image size in terminal character cells.</returns>
    public static ImageSize ConvertToCharacterCells(Stream imageStream)
    {
        using var image = Image.Load<Rgba32>(imageStream);
        return ConvertToCharacterCells(image.Width, image.Height);
    }

    /// <summary>
    /// Converts pixel dimensions to terminal character cells.
    /// Does not apply any constraints, just converts the natural size.
    /// </summary>
    /// <param name="pixelWidth">Width in pixels.</param>
    /// <param name="pixelHeight">Height in pixels.</param>
    /// <returns>Dimensions in terminal character cells.</returns>
    public static ImageSize ConvertToCharacterCells(int pixelWidth, int pixelHeight)
    {
        var cellSize = Compatibility.GetCellSize();

        // Calculate natural dimensions in cells (rounded down to ensure fit)
        var cellWidth = (int)Math.Floor((double)pixelWidth / cellSize.PixelWidth);
        var cellHeight = (int)Math.Floor((double)pixelHeight / cellSize.PixelHeight);

        return new ImageSize(cellWidth, cellHeight);
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    public static ImageSize GetCharacterCellSize(int pixelWidth, int pixelHeight)
    {
        var cellSize = Compatibility.GetCellSize();
        return new ImageSize(
            (int)Math.Floor((double)pixelWidth / cellSize.PixelWidth),
            (int)Math.Floor((double)pixelHeight / cellSize.PixelHeight)
        );
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    public static ImageSize GetCharacterCellSize(Image<Rgba32> image)
        => GetCharacterCellSize(image.Width, image.Height);
    /// <summary>
    /// Gets the resized size in terminal character cells for an image, given max width/height constraints.
    /// Maintains aspect ratio. If both are null, returns the current size.
    /// </summary>
    public static ImageSize GetResizedCharacterCellSize(int pixelWidth, int pixelHeight, int maxCellWidth, int maxCellHeight)
    {
        var cellSize = Compatibility.GetCellSize();

        // natural cell size for the image
        int naturalCellW = Math.Max(1, (int)Math.Ceiling((double)pixelWidth / cellSize.PixelWidth));
        int naturalCellH = Math.Max(1, (int)Math.Ceiling((double)pixelHeight / cellSize.PixelHeight));

        // clamp cell dimensions to window
        int windowWidth = Console.WindowWidth - 2;
        int windowHeight = Console.WindowHeight - 2;
        int maxCellsW = maxCellWidth > 0 ? Math.Min(maxCellWidth, windowWidth) : windowWidth;
        int maxCellsH = maxCellHeight > 0 ? Math.Min(maxCellHeight, windowHeight) : windowHeight;

        // scale to fit max with aspect ratio
        double scaleW = (double)maxCellsW / naturalCellW;
        double scaleH = (double)maxCellsH / naturalCellH;
        // dont upscale
        double scale = Math.Min(1.0, Math.Min(scaleW, scaleH));

        int cellW = Math.Max(1, (int)Math.Floor(naturalCellW * scale));
        int cellH = Math.Max(1, (int)Math.Floor(naturalCellH * scale));

        // sixel boundary adjustments
        int pixelH = cellH * cellSize.PixelHeight;
        // round down to nearest 6
        int sixelAlignedPixelH = pixelH - (pixelH % 6);
        if (sixelAlignedPixelH <= 0) sixelAlignedPixelH = 6;
        int finalCellH = Math.Max(1, sixelAlignedPixelH / cellSize.PixelHeight);

        return new ImageSize(cellW, finalCellH);
    }
    /// <summary>
    /// Testing some other variants of resizing logic.
    /// </summary>
    public static ImageSize GetResizedCharacterCellSizeTest(int pixelWidth, int pixelHeight, int maxCellWidth, int maxCellHeight)
    {
        var cellSize = Compatibility.GetCellSize();

        // natural cell size for the image
        int naturalCellW = (int)Math.Round((double)pixelWidth / cellSize.PixelWidth);
        int naturalCellH = (int)Math.Round((double)pixelHeight / cellSize.PixelHeight);

        // clamp cell dimensions to window
        int windowWidth = Console.WindowWidth - 2;
        int windowHeight = Console.WindowHeight - 2;
        int maxCellsW = maxCellWidth > 0 ? Math.Min(maxCellWidth, windowWidth) : windowWidth;
        int maxCellsH = maxCellHeight > 0 ? Math.Min(maxCellHeight, windowHeight) : windowHeight;

        // scale to fit max with aspect ratio
        double scaleW = Math.Round((double)maxCellsW / naturalCellW);
        double scaleH = Math.Round((double)maxCellsH / naturalCellH);
        // dont upscale
        double scale = Math.Min(1.0, Math.Min(scaleW, scaleH));

        int cellW = Math.Max(1, (int)Math.Round(naturalCellW * scale));
        int cellH = Math.Max(1, (int)Math.Round(naturalCellH * scale));

        // sixel boundary adjustments
        int pixelH = cellH * cellSize.PixelHeight;
        // round down to nearest 6
        int sixelAlignedPixelH = pixelH - (pixelH % 6);
        if (sixelAlignedPixelH <= 0) sixelAlignedPixelH = 6;
        int finalCellH = Math.Max(1, (int)Math.Round((double)sixelAlignedPixelH / cellSize.PixelHeight));

        return new ImageSize(cellW, finalCellH);
    }

    /// <summary>
    /// Gets the resized size in terminal character cells for an image, given max width/height constraints.
    /// Maintains aspect ratio. If both are null, returns the current size.
    /// </summary>
    public static ImageSize GetResizedCharacterCellSize(Image<Rgba32> image, int maxCellWidth, int maxCellHeight)
        => GetResizedCharacterCellSize(image.Width, image.Height, maxCellWidth, maxCellHeight);

    /// <summary>
    /// Gets the constrained terminal image size for the image, applying width/height constraints.
    /// </summary>
    /// <param name="image">The image to size.</param>
    /// <param name="maxWidth">Optional maximum width in character cells.</param>
    /// <param name="maxHeight">Optional maximum height in character cells.</param>
    /// <returns>Constrained image size in terminal character cells.</returns>
    public static ImageSize GetTerminalImageSize(Image<Rgba32> image, int maxWidth, int maxHeight)
    {
        return GetResizedCharacterCellSize(image.Width, image.Height, maxWidth, maxHeight);
    }

    /// <summary>
    /// Gets the constrained terminal image size for the image, applying width/height constraints.
    /// </summary>
    /// <param name="imageStream">The image stream to size.</param>
    /// <param name="maxWidth">Optional maximum width in character cells.</param>
    /// <param name="maxHeight">Optional maximum height in character cells.</param>
    /// <returns>Constrained image size in terminal character cells.</returns>
    public static ImageSize GetTerminalImageSize(Stream imageStream, int maxWidth, int maxHeight)
    {
        using var image = Image.Load<Rgba32>(imageStream);
        return GetResizedCharacterCellSize(image.Width, image.Height, maxWidth, maxHeight);
    }

    /// <summary>
    /// Gets the constrained terminal image size, applying width/height constraints.
    /// </summary>
    /// <param name="pixelWidth">Width in pixels.</param>
    /// <param name="pixelHeight">Height in pixels.</param>
    /// <param name="maxWidth">Optional maximum width in character cells.</param>
    /// <param name="maxHeight">Optional maximum height in character cells.</param>
    /// <returns>Constrained image size in terminal character cells.</returns>
    public static ImageSize GetTerminalImageSize(int pixelWidth, int pixelHeight, int maxWidth, int maxHeight)
    {
        return GetResizedCharacterCellSize(pixelWidth, pixelHeight, maxWidth, maxHeight);
    }

    internal static ImageSize GetTerminalImageSize(this Image<Rgba32> image)
    {
        return ConvertToCharacterCells(image);
    }
}
/// </summary>
///
