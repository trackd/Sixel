using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sixel.Protocols;

public static class KittyGraphics {
    /// <summary>
    /// Converts an image stream to Kitty Graphics Protocol format.
    /// </summary>
    /// <returns>The Kitty Graphics Protocol formatted string.</returns>
    public static string ImageToKitty(Stream imageStream) {
        // Read the raw image data from the stream
        using var ms = new MemoryStream();
        imageStream.CopyTo(ms);
        byte[] imageBytes = ms.ToArray();
        string base64Image = Convert.ToBase64String(imageBytes);
        return ConvertToKittyGraphics(base64Image);
    }
    public static string ImageToKitty(Image<Rgba32> image, ImageSize imageSize) {
        // Use Resizer to handle resizing
        Image<Rgba32> resizedImage = Resizer.ResizeToCharacterCells(image, imageSize, 0);
        // convert the resized image to base64
        using MemoryStream? ms = new();
        resizedImage.SaveAsPng(ms);
        byte[] imageBytes = ms.ToArray();
        string base64Image = Convert.ToBase64String(imageBytes);
        return ConvertToKittyGraphics(base64Image);
    }
    private static string ConvertToKittyGraphics(string base64Image) {
        // basic implementation of kitty graphics protocol
        StringBuilder sb = new();
        int pos = 0;
        while (pos < base64Image.Length) {
            sb.Append(Constants.KittyStart);
            if (pos == 0) {
                sb.Append(Constants.KittyPos);
            }
            int remaining = base64Image.Length - pos;
            int chunkSize = Math.Min(Constants.KittychunkSize, remaining);
            ReadOnlySpan<char> chunk = base64Image.AsSpan(pos, chunkSize);
            pos += chunkSize;
            if (pos < base64Image.Length) {
                sb.Append(Constants.KittyMore);
            }
            else {
                sb.Append(Constants.KittyFinish);
            }
            sb
            .Append(Constants.Divider)
            .Append(chunk)
            .Append(Constants.ST);
        }

        return sb.ToString();
    }
}
