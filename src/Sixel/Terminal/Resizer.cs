using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Terminal;

/// <summary>Fits image content in pixel space before measuring occupied terminal cells.</summary>
public static class Resizer {
    /// <summary>Resizes to the requested cell bounds, preserving aspect ratio.</summary>
    public static (ImageSize Size, Image<Rgba32> ConsoleImage) OldResizeToCharacterCells(
        Image<Rgba32> image, int maxColors, int? RequestedWidth, int? RequestedHeight, bool quantize = false) {
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height,
            RequestedWidth ?? 0, RequestedHeight ?? 0, Compatibility.GetCellSize(), sixel: true);
        ResizeToPixels(image, layout.Pixels, quantize ? maxColors : 0);
        return (layout.Cells, image);
    }

    /// <summary>Fits an image within sixel cell bounds, including six-pixel band occupancy.</summary>
    public static Image<Rgba32> ResizeToCharacterCells(Image<Rgba32> image, ImageSize imageSize, int maxColors) {
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height,
            imageSize.Width, imageSize.Height, Compatibility.GetCellSize(), sixel: true);
        return ResizeToPixels(image, layout.Pixels, maxColors);
    }

    internal static Image<Rgba32> ResizeToPixels(Image<Rgba32> image, Size pixels, int maxColors) {
        if (image.Width != pixels.Width || image.Height != pixels.Height) {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions {
                Mode = ResizeMode.Stretch,
                Size = pixels,
                Sampler = KnownResamplers.Bicubic,
                PremultiplyAlpha = true
            }));
        }
        if (maxColors > 0) {
            image.Mutate(ctx => {
#if NET472
                ctx.Quantize(new OctreeQuantizer(new() { MaxColors = maxColors }));
#else
                ctx.Quantize(new HexadecatreeQuantizer(new() { MaxColors = maxColors }));
#endif
            });
        }
        return image;
    }

    // Pad only after fitting, so explicit whole-cell placement preserves the content aspect ratio.
    internal static void PadToCells(Image<Rgba32> image, ImageSize cells, CellSize cell) {
        int width = checked(cells.Width * cell.PixelWidth);
        int height = checked(cells.Height * cell.PixelHeight);
        if (image.Width != width || image.Height != height) {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions {
                Mode = ResizeMode.BoxPad,
                Position = AnchorPositionMode.TopLeft,
                PadColor = Color.Transparent,
                Size = new Size(width, height)
            }));
        }
    }
}
