using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Terminal;

public static class Resizer
{
    /// <summary>
    /// Resizes an image to fit within the specified terminal character cell dimensions.
    /// </summary>
    /// <param name="image">The image to resize.</param>
    /// <param name="RequestedWidth">The target width in terminal character cells.</param>
    /// <param name="RequestedHeight">The target height in terminal character cells (optional).</param>
    /// <returns>tuple of ImageSize and resized Image stream.</returns>
    public static (ImageSize Size, Image<Rgba32> ConsoleImage) ResizeToCharacterCells(
        Image<Rgba32> image,
        int maxColors,
        int? RequestedWidth,
        int? RequestedHeight,
        bool quantize = false
    )
    {
        var cellSize = Compatibility.GetCellSize();
        int reqWidth = (RequestedWidth > 0) ? RequestedWidth.Value : 0;
        int reqHeight = (RequestedHeight > 0) ? RequestedHeight.Value : 0;

        // If both are zero, do not resize or quantize, just return the current size in cells
        // if (reqWidth == 0 && reqHeight == 0)
        // {
        //     var currentSize = SizeHelper.ConvertToCharacterCells(image.Width, image.Height);
        //     return (currentSize, image);
        // }

        var newSize = SizeHelper.GetResizedCharacterCellSize(image.Width, image.Height, reqWidth, reqHeight);
        // Only resize if the target size is different
        if (image.Width != newSize.PixelWidth || image.Height != newSize.PixelHeight)
        {
            image.Mutate(ctx =>
            {
                ctx.Resize(new ResizeOptions()
                {
                    Sampler = KnownResamplers.Bicubic,
                    Size = new(newSize.PixelWidth, newSize.PixelHeight),
                    PremultiplyAlpha = false,
                });
                if (quantize)
                {
                    ctx.Quantize(new OctreeQuantizer(new()
                    {
                        MaxColors = maxColors,
                    }));
                }
            });
        }
        else if (quantize)
        {
            image.Mutate(ctx =>
            {
                ctx.Quantize(new OctreeQuantizer(new()
                {
                    MaxColors = maxColors,
                }));
            });
        }
        return (newSize, image);
    }
    public static Image<Rgba32> ResizeToCharacterCells(
    Image<Rgba32> image,
    ImageSize imageSize,
    int maxColors,
    bool quantize = false
)
    {
        // Only resize if the target size is different
        if (image.Width != imageSize.PixelWidth || image.Height != imageSize.PixelHeight)
        {
            image.Mutate(ctx =>
            {
                ctx.Resize(new ResizeOptions()
                {
                    Sampler = KnownResamplers.Bicubic,
                    Size = new(imageSize.PixelWidth, imageSize.PixelHeight),
                    PremultiplyAlpha = false,
                });
                if (quantize)
                {
                    ctx.Quantize(new OctreeQuantizer(new()
                    {
                        MaxColors = maxColors,
                    }));
                }
            });
        }
        else if (quantize)
        {
            image.Mutate(ctx =>
            {
                ctx.Quantize(new OctreeQuantizer(new()
                {
                    MaxColors = maxColors,
                }));
            });
        }
        return image;
    }

}
