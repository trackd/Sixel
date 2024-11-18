using Sixel.Terminal;
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
  public static string ImageToKitty(Image<Rgba32> image, int width)
  {
    image.Mutate(ctx => {
      // Some math to get the target size in pixels and reverse it to cell height that it will consume.
      var pixelWidth = width * Compatibility.GetCellSize().PixelWidth;
      var pixelHeight = (int)Math.Round((double)image.Height / image.Width * pixelWidth);
      // Resize the image to the target size
      ctx.Resize(new ResizeOptions() {
        Sampler = KnownResamplers.Bicubic,
        Size = new(pixelWidth, pixelHeight),
        PremultiplyAlpha = false,
      });
    });
    // convert the image to base64
    using MemoryStream? ms = new();
    image.SaveAsPng(ms);
    var imageBytes = ms.ToArray();
    var base64Image = Convert.ToBase64String(imageBytes);
    return ConvertToKittyGraphics(base64Image);
  }
  private static string ConvertToKittyGraphics(string base64Image)
  {
    const int chunkSize = 4096;
    StringBuilder sb = new();
    int pos = 0;
    while (pos < base64Image.Length)
    {
      sb.Append("\x1b_G");
      if (pos == 0)
      {
        sb.Append("a=T,f=100,");
      }
      int remaining = base64Image.Length - pos;
      string chunk = base64Image.Substring(pos, Math.Min(chunkSize, remaining));
      pos += chunk.Length;
      if (pos < base64Image.Length)
      {
        sb.Append("m=1");
      }
      else
      {
        sb.Append("m=0");
      }
      sb.Append(";").Append(chunk).Append("\x1b\\");
    }
    return sb.ToString();
  }

}
