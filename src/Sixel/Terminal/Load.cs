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
      // if (_image.Frames.Count > 1)
      // {
      // move it here?
      //   return Protocols.GifToSixel.LoadGif(imageStream, maxColors, width, 3);
      // }
      return Protocols.Sixel.ImageToSixel(_image, maxColors, width);
      // return ImageToSixel(_image, maxColors, width);
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
        // return ImageToKitty(_image, width);
      }
      return Protocols.KittyGraphics.ImageToKitty(imageStream);
      // return ImageToKitty(imageStream);
    }
    if (imageProtocol == ImageProtocol.InlineImageProtocol || imageProtocol == ImageProtocol.iTerm2)
    {
      return Protocols.InlineImage.ImageToInline(imageStream, width);
    }
    if (imageProtocol == ImageProtocol.Blocks)
    {
        using var _image = Image.Load<Rgba32>(imageStream);
        return Protocols.Blocks.ImageToBlocks(_image, width);
    }
      return null;
  }
}
