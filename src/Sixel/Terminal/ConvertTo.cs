using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sixel.Terminal.Models;
using Sixel.Protocols;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
namespace Sixel.Terminal;

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
    var imageSize = SizeHelper.GetResizedCharacterCellSize(image, width, height);

    // Use the resolved protocol for all logic below
    switch (protocol)
    {
        case ImageProtocol.Sixel:
            if (!autoProtocol.Contains(ImageProtocol.Sixel) && !Compatibility.TerminalSupportsSixel() && !Force) {
                throw new InvalidOperationException("Terminal does not support sixel, override with -Force");
            }
            return (imageSize, Protocols.Sixel.ImageToSixel(image, imageSize, maxColors));

        case ImageProtocol.KittyGraphicsProtocol:
            if (!autoProtocol.Contains(ImageProtocol.KittyGraphicsProtocol) && !Compatibility.TerminalSupportsKitty() && !Force) {
                throw new InvalidOperationException("Terminal does not support Kitty, override with -Force");
            }
            return (imageSize, KittyGraphics.ImageToKitty(image, imageSize));

        case ImageProtocol.InlineImageProtocol:
            if (!autoProtocol.Contains(ImageProtocol.InlineImageProtocol) && !Force) {
                throw new InvalidOperationException("Terminal does not support Inline Image, override with -Force");
            }
            imageStream.Position = 0;
            return (imageSize, InlineImage.ImageToInline(imageStream, imageSize));

        case ImageProtocol.Blocks:
            return (imageSize, Blocks.ImageToBlocks(image, imageSize));

        default:
            throw new InvalidOperationException($"Unsupported image protocol: {protocol}");
    }

    // return (imageSize, data);
  }
}
