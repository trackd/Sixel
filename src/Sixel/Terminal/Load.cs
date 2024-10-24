using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sixel.Terminal.Models;

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
      return Protocols.InlineImage.ImageToInline(imageStream);
    }
    if (imageProtocol == ImageProtocol.KittyGraphicsProtocol)
    {
      throw new NotImplementedException("Kitty Graphics Protocol not implemented.");
    }
    return null;
  }
}
