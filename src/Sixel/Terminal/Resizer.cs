using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Terminal;

/// <summary>
/// Provides methods to resize images to fit within terminal character cell dimensions, with optional color quantization.
/// </summary>
public static class Resizer {
    /// <summary>
    /// Resizes an image to fit within the specified terminal character cell dimensions.
    /// </summary>
    /// <param name="image">The image to resize.</param>
    /// <param name="maxColors">The maximum number of colors to use (for quantization).</param>
    /// <param name="RequestedWidth">The target width in terminal character cells.</param>
    /// <param name="RequestedHeight">The target height in terminal character cells (optional).</param>
    /// <param name="quantize">Whether to quantize the image to reduce colors.</param>
    /// <returns>tuple of ImageSize and resized Image stream.</returns>
    [Obsolete("Use ResizeToCharacterCells instead")]
    internal static (ImageSize Size, Image<Rgba32> ConsoleImage) OldResizeToCharacterCells(
        Image<Rgba32> image,
        int maxColors,
        int? RequestedWidth,
        int? RequestedHeight,
        bool quantize = false
    ) {
        CellSize cellSize = Compatibility.GetCellSize();
        int reqWidth = (RequestedWidth > 0) ? RequestedWidth.Value : 0;
        int reqHeight = (RequestedHeight > 0) ? RequestedHeight.Value : 0;

        // If both are zero, do not resize or quantize, just return the current size in cells
        // if (reqWidth == 0 && reqHeight == 0)
        // {
        //     var currentSize = SizeHelper.ConvertToCharacterCells(image.Width, image.Height);
        //     return (currentSize, image);        // }

        ImageSize newSize = SizeHelper.GetResizedCharacterCellSize(image.Width, image.Height, reqWidth, reqHeight);

        // Calculate pixel dimensions from cell dimensions
        int targetPixelWidth = newSize.Width * cellSize.PixelWidth;
        int targetPixelHeight = newSize.Height * cellSize.PixelHeight;

        // Only resize if the target size is different
        if (image.Width != targetPixelWidth || image.Height != targetPixelHeight) {
            image.Mutate(ctx => {
                ctx.Resize(new ResizeOptions() {
                    // Pads the image to fit the bound of the container without resizing the original source.
                    // When downscaling, performs the same functionality as Pad
                    Mode = ResizeMode.BoxPad,
                    Position = AnchorPositionMode.TopLeft,
                    PadColor = Color.Transparent,
                    // https://en.wikipedia.org/wiki/Bicubic_interpolation
                    // quality goes Bicubic > Bilinear > NearestNeighbor
                    Sampler = KnownResamplers.Bicubic,
                    Size = new(targetPixelWidth, targetPixelHeight),
                    PremultiplyAlpha = false,
                });
                if (quantize) {
                    ctx.Quantize(new OctreeQuantizer(new() {
                        MaxColors = maxColors,
                    }));
                }
            });
        }
        else if (quantize) {
            image.Mutate(ctx => {
                ctx.Quantize(new OctreeQuantizer(new() {
                    MaxColors = maxColors,
                }));
            });
        }
        return (newSize, image);
    }
    /// <summary>
    /// Resizes an image to fit within the specified terminal character cell dimensions.
    /// This method is used when the image size is already known and does not need to be calculated.
    /// </summary>
    /// <param name="image">The image to resize.</param>
    /// <param name="imageSize">The target size in terminal character cells.</param>    /// <param name="maxColors">The maximum number of colors to use (for quantization).</param>
    /// <returns>The resized image.</returns>
    public static Image<Rgba32> ResizeToCharacterCells(
        Image<Rgba32> image,
        ImageSize imageSize,
        int maxColors
    ) {
        CellSize cellSize = Compatibility.GetCellSize();

        // Calculate pixel dimensions from cell dimensions
        int targetPixelWidth = imageSize.Width * cellSize.PixelWidth;
        int targetPixelHeight = imageSize.Height * cellSize.PixelHeight;

        // Only resize if the target size is different
        if (image.Width != targetPixelWidth || image.Height != targetPixelHeight) {
            image.Mutate(ctx => {
                ctx.Resize(new ResizeOptions() {
                    // Fill the exact target pixel extents to match the computed cell size.
                    Mode = ResizeMode.Stretch,
                    // https://en.wikipedia.org/wiki/Bicubic_interpolation
                    // quality goes Bicubic > Bilinear > NearestNeighbor
                    Sampler = KnownResamplers.Bicubic,
                    Size = new(targetPixelWidth, targetPixelHeight),
                    PremultiplyAlpha = false,
                });
                if (maxColors > 0) {
                    ctx.Quantize(new OctreeQuantizer(new() {
                        MaxColors = maxColors,
                    }));
                }
            });
        }
        else if (maxColors > 0) {
            image.Mutate(ctx => {
                ctx.Quantize(new OctreeQuantizer(new() {
                    MaxColors = maxColors,
                }));
            });
        }
        return image;
    }

    /// <summary>
    /// Pads the image height to a multiple of 6 pixels for sixel encoding, without changing the width.
    /// </summary>
    /// <param name="image">The image to pad.</param>
    internal static void PadHeightToMultipleOf6(Image<Rgba32> image) {
        int paddedHeight = (image.Height + 5) / 6 * 6;
        if (paddedHeight == image.Height) {
            return;
        }

        image.Mutate(ctx => {
            ctx.Resize(new ResizeOptions() {
                // Pad only on the bottom to keep the origin anchored.
                Mode = ResizeMode.Pad,
                Position = AnchorPositionMode.TopLeft,
                PadColor = Color.Transparent,
                Size = new(image.Width, paddedHeight),
                PremultiplyAlpha = false,
            });
        });
    }
}
