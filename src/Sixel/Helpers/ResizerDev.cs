using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Terminal;

/// Experimental image resizing helpers used for validating and tuning terminal rendering math.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ResizerDev"/> provides alternative resizing algorithms and size calculations
/// intended primarily for development, testing, and comparison against the main resizing
/// helpers in this library (for example, non-<c>Dev</c> resizer/size helper classes).
/// </para>
/// <para>
/// This API is considered <b>experimental</b>: its behavior and surface area may change
/// between releases without notice. Consumers should prefer the primary, documented
/// resizing helpers for production use, and treat this type as an advanced or diagnostic
/// utility.
/// </para>
internal static class ResizerDev {
    public static Image<Rgba32> ResizeForSixel(
        Image<Rgba32> image,
        ImageSize imageSize,
        int maxColors
    ) {
        CellSize cellSize = Compatibility.GetCellSize();
        int targetPixelWidth = imageSize.Width * cellSize.PixelWidth;
        int targetPixelHeight = imageSize.Height * cellSize.PixelHeight;
        int sixelAlignedHeight = (targetPixelHeight + 5) / 6 * 6;
        return ResizeExact(image, targetPixelWidth, sixelAlignedHeight, maxColors);
    }

    public static Image<Rgba32> ResizeForKitty(
        Image<Rgba32> image,
        ImageSize imageSize
    ) {
        CellSize cellSize = Compatibility.GetCellSize();
        int targetPixelWidth = imageSize.Width * cellSize.PixelWidth;
        int targetPixelHeight = imageSize.Height * cellSize.PixelHeight;
        return ResizeExact(image, targetPixelWidth, targetPixelHeight, maxColors: 0);
    }

    public static Image<Rgba32> ResizeForBlocks(
        Image<Rgba32> image,
        ImageSize imageSize
    ) {
        int targetPixelWidth = imageSize.Width;
        int targetPixelHeight = imageSize.Height * 2;
        return ResizeExact(image, targetPixelWidth, targetPixelHeight, maxColors: 0);
    }

    public static Image<Rgba32> ResizeForBraille(
        Image<Rgba32> image,
        ImageSize imageSize
    ) {
        int targetPixelWidth = imageSize.Width * 2;
        int targetPixelHeight = imageSize.Height * 4;
        return ResizeExact(image, targetPixelWidth, targetPixelHeight, maxColors: 0);
    }

    private static Image<Rgba32> ResizeExact(
        Image<Rgba32> image,
        int targetPixelWidth,
        int targetPixelHeight,
        int maxColors
    ) {
        bool needsResize = image.Width != targetPixelWidth || image.Height != targetPixelHeight;
        bool needsQuantize = maxColors > 0;

        if (!needsResize && !needsQuantize) {
            return image;
        }

        image.Mutate(ctx => {
            if (needsResize) {
                ctx.Resize(new ResizeOptions() {
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Bicubic,
                    Size = new(targetPixelWidth, targetPixelHeight),
                    PremultiplyAlpha = false,
                });
            }

            if (needsQuantize) {
                ctx.Quantize(new OctreeQuantizer(new() {
                    MaxColors = maxColors,
                }));
            }
        });

        return image;
    }

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
    /// <param name="imageSize">The target size in terminal character cells.</param>
    /// <param name="maxColors">The maximum number of colors to use (for quantization).</param>
    /// <param name="padHeightToMultipleOf6">When true, pad the final height to the next multiple of 6 pixels for sixel encoding without stretching the content.</param>
    /// <returns>The resized image.</returns>
    public static Image<Rgba32> ResizeToCharacterCells(
        Image<Rgba32> image,
        ImageSize imageSize,
        int maxColors,
        bool padHeightToMultipleOf6 = false
    ) {
        return padHeightToMultipleOf6
            ? ResizeForSixel(image, imageSize, maxColors)
            : ResizeExact(
                image,
                imageSize.Width * Compatibility.GetCellSize().PixelWidth,
                imageSize.Height * Compatibility.GetCellSize().PixelHeight,
                maxColors
            );
    }
}
