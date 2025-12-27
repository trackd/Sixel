using System.Globalization;
using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Protocols;

public static class InlineImage {
    /// <summary>
    /// Converts an image to an inline image protocol string.
    /// </summary>
    internal static string ImageToInline(Stream image, int width = 0, int height = 0) {
        byte[] imageBytes;
        if (image.CanSeek) {
            // If the stream supports seeking, read it directly
            image.Seek(0, SeekOrigin.Begin);
            imageBytes = new byte[image.Length];
#if NET472
            int bytesRead = 0;
            int totalBytesRead = 0;
            while (totalBytesRead < imageBytes.Length &&
                    (bytesRead = image.Read(imageBytes, totalBytesRead, imageBytes.Length - totalBytesRead)) > 0) {
                totalBytesRead += bytesRead;
            }
            // Only resize if we couldn't read the full stream (very rare if Length is accurate)
            if (totalBytesRead != imageBytes.Length) {
                Array.Resize(ref imageBytes, totalBytesRead);
            }
#else
            // Use ReadExactly in .NET 6+ or handle partial reads in older versions
            image.ReadExactly(imageBytes, 0, imageBytes.Length);
#endif
        }
        else {
            // For non-seekable streams, using CopyTo is already efficient
            using MemoryStream ms = new();
            image.CopyTo(ms);
            imageBytes = ms.ToArray();
        }
        ReadOnlySpan<char> base64Image = Convert.ToBase64String(imageBytes).AsSpan();
        string size = imageBytes.Length.ToString(CultureInfo.InvariantCulture);
        string widthString = width > 0 ? $"width={width};" : "width=auto;";
        string heightString = height > 0 ? $"height={height};" : "height=auto;";
        StringBuilder iip = new();
        /// hide cursor
        iip.Append(Constants.HideCursor)
        .Append(Constants.InlineImageStart)
        .Append("1337;File=inline=1;")
        .Append("size=" + size + ";")
        .Append(widthString)
        .Append(heightString)
        .Append("preserveAspectRatio=1:")
        // .Append("preserveAspectRatio=1;")
        // .Append("doNotMoveCursor=1:")
        .Append(base64Image)
        .Append(Constants.InlineImageEnd)
        .Append(Constants.ShowCursor);
        return iip.ToString();
    }
}
