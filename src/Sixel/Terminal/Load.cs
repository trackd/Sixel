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
  /// implement type <T> to allow for more image types..
  public static string? ConsoleImage(ImageProtocol imageProtocol, Stream imageStream, int maxColors, int width, bool Force)
  {
    if (imageProtocol == ImageProtocol.Sixel)
    {
      if (Compatibility.TerminalSupportsSixel() == false && Force == false)
      {
        throw new InvalidOperationException("Terminal does not support sixel, override with -Force (Windows Terminal needs Preview release for Sixel Support)");
      }
      using var _image = Image.Load<Rgba32>(imageStream);
      // move it here? decisions decisions.. we could yeet the gif cmdlet and just make it a param.
      // check if the image is a gif
      // if (_image.Frames.Count > 1)
      // {
      //   return Protocols.GifToSixel.LoadGif(imageStream, maxColors, width, 3);
      // }
      return Protocols.Sixel.ImageToSixel(_image, maxColors, width);
    }
    if (imageProtocol == ImageProtocol.InlineImageProtocol)
    {
      return Protocols.InlineImage.ImageToInline(imageStream, width);
    }
    if (imageProtocol == ImageProtocol.KittyGraphicsProtocol)
    {
      if (Compatibility.TerminalSupportsKitty() == false && Force == false)
      {
        throw new InvalidOperationException("Terminal does not support Kitty, override with -Force");
      }
      if (width > 0)
      {
        // we need to resize the image to the target width
        using var _image = Image.Load<Rgba32>(imageStream);
        return Protocols.KittyGraphics.ImageToKitty(_image, width);
      }
      return Protocols.KittyGraphics.ImageToKitty(imageStream);
    }
    // if (imageProtocol == ImageProtocol.None)
    // {
    //   using var _image = Image.Load<Rgba32>(imageStream);
    //   return Protocols.AsciiGenerator.ImageToAscii(_image, width);
    // }
    return null;
  }
}
