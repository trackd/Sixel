using Sixel.Protocols;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
namespace Sixel.Terminal;

/// <summary>
/// Provides methods to load and convert images to terminal-compatible formats using various image protocols.
/// </summary>
public static class ConvertTo
{
    /// <summary>
    /// Load an image and convert it to a terminal compatible format.
    /// </summary>
    /// <param name="imageProtocol">The image protocol to use for conversion.</param>
    /// <param name="imageStream">The image stream to convert.</param>
    /// <param name="maxColors">The maximum number of colors to use (for Sixel).</param>
    /// <param name="width">The target width in character cells, or 0 to use default size.</param>
    /// <param name="height">The target height in character cells, or 0 to maintain aspect ratio.</param>
    /// <param name="Force">Whether to force conversion even if terminal doesn't support the protocol.</param>
    /// <returns>A tuple containing the image size and the converted image data.</returns>
    public static (ImageSize Size, string Data) ConsoleImage(
      ImageProtocol imageProtocol,
      Stream imageStream,
      int maxColors,
      int width = 0,
      int height = 0,
      bool Force = false
    )
    {
        /// this is a guess at the protocol based on the environment variables and VT responses.
        /// the parameter `imageProtocol` is the chosen protocol, we need to see if that is supported.
        var autoProtocol = Compatibility.GetTerminalInfo().Protocol;

        // Improved: If Auto, select the best supported protocol by priority (Kitty > Sixel > Inline > Blocks)
        ImageProtocol protocol = imageProtocol;
        if (imageProtocol == ImageProtocol.Auto)
        {
            if (autoProtocol.Contains(ImageProtocol.KittyGraphicsProtocol))
                protocol = ImageProtocol.KittyGraphicsProtocol;
            else if (autoProtocol.Contains(ImageProtocol.Sixel))
                protocol = ImageProtocol.Sixel;
            else if (autoProtocol.Contains(ImageProtocol.InlineImageProtocol))
                protocol = ImageProtocol.InlineImageProtocol;
            else
                protocol = ImageProtocol.Blocks;
        }
        // Load the image once to avoid duplicate loading
        using var image = Image.Load<Rgba32>(imageStream);

        // For Sixel and Blocks: use natural sizing if no constraints, otherwise apply constraints
        ImageSize constrainedSize;
        if (width == 0 && height == 0)
        {
            // No constraints specified - use natural image size
            constrainedSize = SizeHelper.ConvertToCharacterCells(image);
        }
        else
        {
            // Constraints specified - apply resizing logic
            constrainedSize = SizeHelper.GetResizedCharacterCellSize(image, width, height);
        }

        // Use the resolved protocol for all logic below
        switch (protocol)
        {
            case ImageProtocol.Sixel:
                if (!autoProtocol.Contains(ImageProtocol.Sixel) && !Compatibility.TerminalSupportsSixel() && !Force)
                {
                    throw new InvalidOperationException("Terminal does not support sixel, override with -Force");
                }
                // Resize first to get actual pixel dimensions, then compute final cell size from the resized image.
                var resized = Resizer.ResizeToCharacterCells(image, constrainedSize, maxColors);
                var finalSize = SizeHelper.GetCharacterCellSize(resized);
                var frame = resized.Frames[0];
                var data = Protocols.Sixel.FrameToSixelString(frame);
                return (finalSize, data);

            case ImageProtocol.KittyGraphicsProtocol:
                if (!autoProtocol.Contains(ImageProtocol.KittyGraphicsProtocol) && !Compatibility.TerminalSupportsKitty() && !Force)
                {
                    throw new InvalidOperationException("Terminal does not support Kitty, override with -Force");
                }
                // Pass raw width/height to Kitty - it handles 0 values properly
                var kittySize = new ImageSize(width, height);
                return (kittySize, KittyGraphics.ImageToKitty(image, kittySize));

            case ImageProtocol.InlineImageProtocol:
                if (!autoProtocol.Contains(ImageProtocol.InlineImageProtocol) && !Force)
                {
                    throw new InvalidOperationException("Terminal does not support Inline Image, override with -Force");
                }
                imageStream.Position = 0;
                // Pass raw width/height to InlineImage - 0 values become "auto"
                var inlineSize = new ImageSize(width, height);
                return (inlineSize, InlineImage.ImageToInline(imageStream, width, height));

            case ImageProtocol.Blocks:
                return (constrainedSize, Blocks.ImageToBlocks(image, constrainedSize));

            default:
                throw new InvalidOperationException($"Unsupported image protocol: {protocol}");
        }
    }
}
