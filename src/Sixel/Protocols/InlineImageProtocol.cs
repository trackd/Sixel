using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Text;
using System.Globalization;

namespace Sixel.Protocols;

public static class InlineImage
{
  /// <summary>
  /// Converts an image to an inline image protocol string.
  /// </summary>
  internal static string ImageToInline(Stream image, ImageSize imageSize)
  {
    byte[] imageBytes;
    if (image.CanSeek)
    {
      // If the stream supports seeking, read it directly
      image.Seek(0, SeekOrigin.Begin);
      imageBytes = new byte[image.Length];
#if NET472
        int bytesRead = 0;
        int totalBytesRead = 0;
        while (totalBytesRead < imageBytes.Length &&
                (bytesRead = image.Read(imageBytes, totalBytesRead, imageBytes.Length - totalBytesRead)) > 0)
        {
            totalBytesRead += bytesRead;
        }
        // Only resize if we couldn't read the full stream (very rare if Length is accurate)
        if (totalBytesRead != imageBytes.Length)
        {
            Array.Resize(ref imageBytes, totalBytesRead);
        }
#else
      // Use ReadExactly in .NET 6+ or handle partial reads in older versions
      image.ReadExactly(imageBytes, 0, imageBytes.Length);
#endif
    }
    else
    {
      // For non-seekable streams, using CopyTo is already efficient
      using MemoryStream ms = new();
      image.CopyTo(ms);
      imageBytes = ms.ToArray();
    }
    var base64Image = Convert.ToBase64String(imageBytes).AsSpan();
    string size = imageBytes.Length.ToString(CultureInfo.InvariantCulture);
    string widthString = imageSize.CellWidth > 0 ? $"width={imageSize.CellWidth};" : "width=auto;";
    string heightString = imageSize.CellHeight > 0 ? $"height={imageSize.CellHeight};" : "height=auto;";
    StringBuilder iip = new();
    iip.Append(Constants.HideCursor)
        .Append(Constants.InlineImageStart)
        .Append("1337;File=inline=1;")
        .Append("size=" + size + ";")
        .Append(widthString)
        .Append(heightString)
        .Append("preserveAspectRatio=1;")
        .Append("doNotMoveCursor=1:")
        .Append(base64Image)
        .Append(Constants.InlineImageEnd)
        .Append(Constants.ShowCursor);
    return iip.ToString();
  }
  internal static string ImageToInlinev1(Stream image, int width = 0, int height = 0)
  {
    using var ms = new MemoryStream();
    image.CopyTo(ms);
    var imageBytes = ms.ToArray();
    var base64Image = Convert.ToBase64String(imageBytes).AsSpan();
    string size = imageBytes.Length.ToString(CultureInfo.InvariantCulture);
    string widthString = width > 0 ? $"width={width};" : "width=auto;";
    string heightString = height > 0 ? $"height={height};" : "height=auto;";
    var iip = new StringBuilder();
    iip.Append(Constants.InlineImageStart)
      .Append("1337;File=inline=1;")
      .Append("size=" + size + ";")
      .Append(widthString)
      .Append(heightString)
      .Append("preserveAspectRatio=1:")
      .Append(base64Image)
      .Append(Constants.InlineImageEnd);
    return iip.ToString();
  }
}
