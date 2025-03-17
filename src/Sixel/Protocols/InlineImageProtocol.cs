using System.Text;
using Sixel.Terminal;

namespace Sixel.Protocols;

public static class InlineImage
{
  /// <summary>
  /// Converts an image to an inline image protocol string.
  /// </summary>
  internal static string ImageToInline(Stream image, int width = 0)
  {
    byte[] imageBytes;
    if (image.CanSeek)
    {
      // If the stream supports seeking, read it directly
      image.Seek(0, SeekOrigin.Begin);
      imageBytes = new byte[image.Length];
      image.Read(imageBytes, 0, imageBytes.Length);
    }
    else
    {
      // If the stream does not support seeking, copy it to a MemoryStream
      using MemoryStream ms = new();
      image.CopyTo(ms);
      imageBytes = ms.ToArray();
    }
    string base64Image = Convert.ToBase64String(imageBytes);
    string size = imageBytes.Length.ToString();
    string widthString = width > 0 ? $"width={width};" : "width=auto;";
    StringBuilder iip = new();
    iip.Append(Constants.HideCursor)
        .Append(Constants.InlineImageStart)
        .Append("1337;File=inline=1;")
        .Append("size=" + size + ";")
        .Append(widthString)
        .Append("height=auto;")
        .Append("preserveAspectRatio=1;")
        .Append("doNotMoveCursor=1:")
        .Append(base64Image)
        .Append(Constants.InlineImageEnd)
        .Append(Constants.ShowCursor);
    return iip.ToString();
  }
  internal static string ImageToInlinev1(Stream image, int width = 0)
  {
    using var ms = new MemoryStream();
    image.CopyTo(ms);
    var imageBytes = ms.ToArray();
    var base64Image = Convert.ToBase64String(imageBytes);
    string size = imageBytes.Length.ToString();
    string widthString = width > 0 ? $"width={width};" : "width=auto;";
    var iip = new StringBuilder();
    iip.Append(Constants.InlineImageStart)
      .Append("1337;File=inline=1;")
      .Append("size=" + size + ";")
      .Append(widthString)
      .Append("height=auto;")
      .Append("preserveAspectRatio=1:")
      .Append(base64Image)
      .Append(Constants.InlineImageEnd);
    return iip.ToString();
  }
}
