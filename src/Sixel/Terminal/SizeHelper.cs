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
        return GetCharacterCellSize(image.Width, image.Height);
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
        return GetCharacterCellSize(image.Width, image.Height);
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    public static ImageSize GetCharacterCellSize(int pixelWidth, int pixelHeight)
    {
        var cellSize = Compatibility.GetCellSize();
        return new ImageSize(
            (int)Math.Ceiling((double)pixelWidth / cellSize.PixelWidth),
            (int)Math.Ceiling((double)pixelHeight / cellSize.PixelHeight)
        );
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    public static ImageSize GetCharacterCellSize(Image<Rgba32> image)
        => GetCharacterCellSize(image.Width, image.Height);
    /// <summary>
    /// Gets the resized size in terminal character cells for an image, given max width/height constraints.
    /// Maintains aspect ratio and ensures proper sixel alignment (multiples of 6 pixels).
    /// </summary>
    public static ImageSize GetResizedCharacterCellSize(int pixelWidth, int pixelHeight, int maxCellWidth, int maxCellHeight)
    {
        var cellSize = Compatibility.GetCellSize();

        // Calculate natural cell dimensions
        var naturalSize = GetCharacterCellSize(pixelWidth, pixelHeight);

        // Apply window constraints
        int windowWidth = Console.WindowWidth - 2;
        int windowHeight = Console.WindowHeight - 2;
        int maxCellsW = maxCellWidth > 0 ? Math.Min(maxCellWidth, windowWidth) : windowWidth;
        int maxCellsH = maxCellHeight > 0 ? Math.Min(maxCellHeight, windowHeight) : windowHeight;

        // Calculate scale to fit within constraints while maintaining aspect ratio
        double scaleW = (double)maxCellsW / naturalSize.Width;
        double scaleH = (double)maxCellsH / naturalSize.Height;
        double scale = Math.Min(scaleW, scaleH); // Allow upscaling within terminal bounds

        // Apply scaling
        int cellW = Math.Max(1, (int)Math.Floor(naturalSize.Width * scale));
        int cellH = Math.Max(1, (int)Math.Floor(naturalSize.Height * scale));

        // Calculate the pixel height and round UP to the nearest multiple of 6
        int targetPixelH = cellH * cellSize.PixelHeight;
        int sixelAlignedPixelH = (targetPixelH + 5) / 6 * 6; // Round up to multiple of 6

        // Convert back to cells, ensuring we have at least 1 cell
        int finalCellH = Math.Max(1, (int)Math.Ceiling((double)sixelAlignedPixelH / cellSize.PixelHeight));

        return new ImageSize(cellW, finalCellH);
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
