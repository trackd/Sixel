using Sixel.Protocols;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
namespace Sixel.Terminal;

/// <summary>
/// Provides methods to load and convert images to terminal-compatible formats using various image protocols.
/// </summary>
public static class ConvertTo {
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
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static (ImageSize Size, string Data) ConsoleImage(
        ImageProtocol imageProtocol,
        Stream imageStream,
        int maxColors,
        int width = 0,
        int height = 0,
        bool Force = false
    ) {
        /// this is a guess at the protocol based on the environment variables and VT responses.
        /// the parameter `imageProtocol` is the chosen protocol, we need to see if that is supported.
        ImageProtocol[] autoProtocol = Compatibility.GetTerminalInfo().Protocol;

        // Improved: If Auto, select the best supported protocol by priority (Sixel > Kitty > Inline > Blocks)
        ImageProtocol protocol = imageProtocol;
        if (imageProtocol == ImageProtocol.Auto) {
            protocol = autoProtocol.Contains(ImageProtocol.Sixel)
                ? ImageProtocol.Sixel
                : autoProtocol.Contains(ImageProtocol.KittyGraphicsProtocol)
                ? ImageProtocol.KittyGraphicsProtocol
                : autoProtocol.Contains(ImageProtocol.InlineImageProtocol)
                ? ImageProtocol.InlineImageProtocol
                : ImageProtocol.Blocks;
        }
        // Load the image once to avoid duplicate loading
        using var image = Image.Load<Rgba32>(imageStream);

        // Use the resolved protocol for all logic below
        switch (protocol) {
            case ImageProtocol.Sixel:
                if (!autoProtocol.Contains(ImageProtocol.Sixel) && !Compatibility.TerminalSupportsSixel() && !Force) {
                    throw new InvalidOperationException("Terminal does not support sixel, override with -Force");
                }
                return Protocols.Sixel.ImageToSixel(image, maxColors, width, height);

            case ImageProtocol.KittyGraphicsProtocol:
                if (!autoProtocol.Contains(ImageProtocol.KittyGraphicsProtocol) && !Compatibility.TerminalSupportsKitty() && !Force) {
                    throw new InvalidOperationException("Terminal does not support Kitty, override with -Force");
                }
                return KittyGraphics.ImageToKitty(image, width, height);

            case ImageProtocol.InlineImageProtocol:
                if (!autoProtocol.Contains(ImageProtocol.InlineImageProtocol) && !Force) {
                    throw new InvalidOperationException("Terminal does not support Inline Image, override with -Force");
                }
                imageStream.Position = 0;
                // Pass raw width/height to InlineImage - 0 values become "auto"
                var inlineSize = new ImageSize(width, height);
                return (inlineSize, InlineImage.ImageToInline(imageStream, width, height));

            case ImageProtocol.Blocks:
                return Blocks.ImageToBlocks(image, width, height);

            case ImageProtocol.Braille:
                return Braille.ImageToBraille(image, width, height);

            case ImageProtocol.Auto:
                throw new InvalidOperationException("Auto protocol should have been resolved");

            default:
                throw new ArgumentOutOfRangeException(nameof(imageProtocol), imageProtocol, "Unknown image protocol");
        }
    }
}
