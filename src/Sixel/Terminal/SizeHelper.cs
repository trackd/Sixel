using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Terminal;

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

        return new ImageSize(cellWidth, cellHeight, cellWidth * cellSize.PixelWidth, cellHeight * cellSize.PixelHeight);
    }

    /// <summary>
    /// Gets the current size of an image in terminal character cells (no resizing, just analysis).
    /// </summary>
    public static ImageSize GetCharacterCellSize(int pixelWidth, int pixelHeight)
    {
        var cellSize = Compatibility.GetCellSize();
        return new ImageSize(
            (int)Math.Floor((double)pixelWidth / cellSize.PixelWidth),
            (int)Math.Floor((double)pixelHeight / cellSize.PixelHeight),
            pixelWidth,
            pixelHeight
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

        // override the maxCellWidth and maxCellHeight if they are bigger than the terminal window size.
        int WindowWidth = Console.WindowWidth - 2;
        int WindowHeight = Console.WindowHeight - 2;
        if (maxCellHeight > WindowHeight)
        {
            maxCellHeight = WindowHeight;
        }
        if (maxCellWidth > WindowWidth)
        {
            maxCellWidth = WindowWidth;
        }
        // If no constraints, match old logic: just floor to cell size, no Sixel rounding here
        if (maxCellWidth == 0 && maxCellHeight == 0)
        {
            int adjustedCellW = Math.Max(1, (int)Math.Floor((double)pixelWidth / cellSize.PixelWidth));
            int adjustedCellH = Math.Max(1, (int)Math.Floor((double)pixelHeight / cellSize.PixelHeight));
            int adjustedPixelWidth = adjustedCellW * cellSize.PixelWidth;
            int adjustedPixelHeight = adjustedCellH * cellSize.PixelHeight;
            return new ImageSize(adjustedCellW, adjustedCellH, adjustedPixelWidth, adjustedPixelHeight);
        }

        // If only one constraint is given, the other is unconstrained (int.MaxValue), so aspect ratio is always preserved
        // The scale is chosen so that neither dimension exceeds its constraint, and aspect ratio is always correct

        // Calculate the pixel bounding box
        int maxPixelWidth = maxCellWidth > 0 ? maxCellWidth * cellSize.PixelWidth : int.MaxValue;
        int maxPixelHeight = maxCellHeight > 0 ? maxCellHeight * cellSize.PixelHeight : int.MaxValue;

        // Scale to fit within the pixel bounding box, preserving aspect ratio
        double scale = Math.Min((double)maxPixelWidth / pixelWidth, (double)maxPixelHeight / pixelHeight);

        if (scale > 1.0)
        {
            // Don't upscale
            scale = 1.0;
        }
        int newPixelWidth = (int)Math.Round(pixelWidth * scale);
        int newPixelHeight = (int)Math.Round(pixelHeight * scale);

        // No Sixel rounding here; just preserve aspect and bounding box

        int newCellW = Math.Max(1, (int)Math.Floor((double)newPixelWidth / cellSize.PixelWidth));
        int newCellH = Math.Max(1, (int)Math.Floor((double)newPixelHeight / cellSize.PixelHeight));
        // Prevent upscaling: clamp to original cell size
        newCellW = Math.Min(newCellW, (int)Math.Floor((double)pixelWidth / cellSize.PixelWidth));
        newCellH = Math.Min(newCellH, (int)Math.Floor((double)pixelHeight / cellSize.PixelHeight));
        return new ImageSize(newCellW, newCellH, newPixelWidth, newPixelHeight);
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
