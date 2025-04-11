using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sixel.Terminal.Models;
using Sixel.Protocols;

namespace Sixel.Terminal;

public static class ConvertTo
{
  /// <summary>
  /// Load an image and convert it to a terminal compatible format.
  /// </summary>
  /// implement type <T> to allow for more image types..
  public static string? ConsoleImage(ImageProtocol imageProtocol, Stream imageStream, int maxColors, int width, bool Force)
  {
    /// this is a guess at the protocol based on the enviroment variables and VT responses.
    /// imageProtocol is the chosen protocol.
    ImageProtocol[] autoProtocol = Compatibility.GetTerminalInfo().Protocol ?? [];
    if (imageProtocol == ImageProtocol.Sixel)
    {
      if (!autoProtocol.Contains(ImageProtocol.Sixel) && Compatibility.TerminalSupportsSixel() == false && Force == false)
      {
        throw new InvalidOperationException("Terminal does not support sixel, override with -Force");
      }
      using var _image = Image.Load<Rgba32>(imageStream);
      return Protocols.Sixel.ImageToSixel(_image, maxColors, width);
    }
    if (imageProtocol == ImageProtocol.KittyGraphicsProtocol)
    {
      if (!autoProtocol.Contains(ImageProtocol.KittyGraphicsProtocol) && Compatibility.TerminalSupportsKitty() == false && Force == false)
      {
        throw new InvalidOperationException("Terminal does not support Kitty, override with -Force");
      }
      if (width > 0)
      {
        // we need to resize the image to the target width
        using var _image = Image.Load<Rgba32>(imageStream);
        return KittyGraphics.ImageToKitty(_image, width);
      }
      return KittyGraphics.ImageToKitty(imageStream);
    }
    if (imageProtocol == ImageProtocol.InlineImageProtocol)
    {
      if (!autoProtocol.Contains(ImageProtocol.InlineImageProtocol) && Force == false)
      {
        throw new InvalidOperationException("Terminal does not support Inline Image, override with -Force");
      }
      return InlineImage.ImageToInline(imageStream, width);
    }
    if (imageProtocol == ImageProtocol.Blocks)
    {
        using var _image = Image.Load<Rgba32>(imageStream);
        return Blocks.ImageToBlocks(_image, width);
    }
      return null;
  }
}
