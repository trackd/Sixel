using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sixel.Terminal.Models;
using Sixel.Protocols;

namespace Sixel.Terminal;

public static class Load
{
  /// <summary>
  /// Load an image and convert it to a terminal compatible format.
  /// </summary>
  public static string? ConsoleImage(ImageProtocol imageProtocol, Stream imageStream, int maxColors, int width, bool Force)
  {
    if (imageProtocol == ImageProtocol.Sixel)
    {
      if (Compatibility.TerminalSupportsSixel() == false && Force == false)
      {
        throw new InvalidOperationException("Terminal does not support sixel, override with -Force (Windows Terminal needs Preview release for Sixel Support)");
      }
      using var _image = Image.Load<Rgba32>(imageStream);
      return Protocols.Sixel.ImageToSixel(_image, maxColors, width);
    }
    if (imageProtocol == ImageProtocol.InlineImageProtocol)
    {
      return Protocols.InlineImage.ImageToInline(imageStream, width);
    }
    if (imageProtocol == ImageProtocol.KittyGraphicsProtocol)
    {
      if (width > 0)
      {
        // we need to resize the image to the target width
        using var _image = Image.Load<Rgba32>(imageStream);
        return Protocols.KittyGraphics.ImageToKitty(_image, width);
      }
      return Protocols.KittyGraphics.ImageToKitty(imageStream);
    }
    return null;
  }
}
