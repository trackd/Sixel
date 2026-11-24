using Sixel.Protocols;
using Sixel.Terminal.Models;
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
                : autoProtocol.Contains(ImageProtocol.InlineImageProtocol) ? ImageProtocol.InlineImageProtocol : ImageProtocol.Blocks;
        }
        // Load the image once to avoid duplicate loading
        using var image = ImageBackend.Load(imageStream);

        CellSize cell = Compatibility.GetCellSize();
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height, width, height,
            cell, sixel: protocol == ImageProtocol.Sixel);
        ImageSize constrainedSize = layout.Cells;

        // Use the resolved protocol for all logic below
        switch (protocol) {
            case ImageProtocol.Sixel:
                if (!autoProtocol.Contains(ImageProtocol.Sixel) && !Compatibility.TerminalSupportsSixel() && !Force) {
                    throw new InvalidOperationException("Terminal does not support sixel, override with -Force");
                }
                // Raster dimensions and occupied cells come from the same layout.
                image.Resize(layout.Pixels.Width, layout.Pixels.Height, maxColors);
                ImageSize finalSize = layout.Cells;
                string data = image.SixelFrame(0);
                return (finalSize, ImagePlacement.Wrap(ImagePlacement.SixelModes + data, finalSize.Height));

            case ImageProtocol.KittyGraphicsProtocol:
                if (!autoProtocol.Contains(ImageProtocol.KittyGraphicsProtocol) && !Compatibility.TerminalSupportsKitty() && !Force) {
                    throw new InvalidOperationException("Terminal does not support Kitty, override with -Force");
                }
                // Explicit placement matches the padded raster exactly.
                ImageSize kittySize = constrainedSize;
                image.Resize(layout.Pixels.Width, layout.Pixels.Height, 0);
                image.Pad(kittySize, cell);
                return (kittySize, ImagePlacement.Wrap(KittyGraphics.Encode(image, kittySize), kittySize.Height));

            case ImageProtocol.InlineImageProtocol:
                if (!autoProtocol.Contains(ImageProtocol.InlineImageProtocol) && !Force) {
                    throw new InvalidOperationException("Terminal does not support Inline Image, override with -Force");
                }
                image.Resize(layout.Pixels.Width, layout.Pixels.Height, 0);
                image.Pad(layout.Cells, cell);
                using (var encoded = new MemoryStream(image.InlineBytes())) {
                    string inline = InlineImage.ImageToInline(encoded, layout.Cells.Width, layout.Cells.Height);
                    return (layout.Cells, ImagePlacement.Wrap(inline, layout.Cells.Height));
                }

            case ImageProtocol.Blocks:
                return image.Text(constrainedSize, braille: false);

            case ImageProtocol.Braille:
                return image.Text(constrainedSize, braille: true);
            case ImageProtocol.Auto:
                // Defensive assertion: the Auto protocol should have been resolved to a concrete protocol above.
                // Reaching this case indicates a logic error in the protocol resolution code.
                throw new InvalidOperationException("Auto protocol should have been resolved");
            default:
                throw new InvalidOperationException($"Unsupported image protocol: {protocol}");
        }
    }
}
