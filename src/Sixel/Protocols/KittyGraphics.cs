using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sixel.Protocols;

public static class KittyGraphics
{
  /// <summary>
  /// Converts an image stream to Kitty Graphics Protocol format.
  /// </summary>
  /// <returns>The Kitty Graphics Protocol formatted string.</returns>
  public static string ImageToKitty(Stream imageStream)
  {
    // Read the raw image data from the stream
    using var ms = new MemoryStream();
    imageStream.CopyTo(ms);
    var imageBytes = ms.ToArray();
    var base64Image = Convert.ToBase64String(imageBytes);
    return ConvertToKittyGraphics(base64Image);
  }
  public static string ImageToKitty(Image<Rgba32> image, ImageSize imageSize)
  {
    // Use Resizer to handle resizing
    var resizedImage = Resizer.ResizeToCharacterCells(image, imageSize, 0);
    // convert the resized image to base64
    using MemoryStream? ms = new();
    resizedImage.SaveAsPng(ms);
    var imageBytes = ms.ToArray();
    var base64Image = Convert.ToBase64String(imageBytes);
    return ConvertToKittyGraphics(base64Image);
  }
  private static string ConvertToKittyGraphics(string base64Image)
  {
    // basic implementation of kitty graphics protocol
    StringBuilder sb = new();
    int pos = 0;
    while (pos < base64Image.Length)
    {
      sb.Append(Constants.KittyStart);
      if (pos == 0)
      {
        sb.Append(Constants.KittyPos);
      }
      int remaining = base64Image.Length - pos;
      string chunk = base64Image.Substring(pos, Math.Min(Constants.KittychunkSize, remaining));
      pos += chunk.Length;
      if (pos < base64Image.Length)
      {
        sb.Append(Constants.KittyMore);
      }
      else
      {
        sb.Append(Constants.KittyFinish);
      }
      sb.Append(Constants.Divider)
        .Append(chunk)
        .Append(Constants.ST);
    }

    return sb.ToString();
  }
}
