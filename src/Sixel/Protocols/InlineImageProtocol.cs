using Sixel.Terminal;
using System.Text;

namespace Sixel.Protocols;

public static class InlineImage
{
  /// <summary>
  /// Converts an image to an inline image protocol string.
  /// </summary>
  internal static string ImageToInline(Stream image)
  {
    using var ms = new MemoryStream();
    image.CopyTo(ms);
    var imageBytes = ms.ToArray();
    var base64Image = Convert.ToBase64String(imageBytes);
    var iip = new StringBuilder();
    iip.Append(Constants.INLINEIMAGESTART)
      .Append("1337;File=inline=1")
      .Append(";width=auto")
      .Append(";height=auto")
      .Append(";preserveAspectRatio=1:")
      .Append(base64Image)
      .Append(Constants.INLINEIMAGEEND);
    return iip.ToString();
  }
}
