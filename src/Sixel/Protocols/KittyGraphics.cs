using System.Globalization;
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
    public static (ImageSize Size, string Data) ImageToKitty(Image<Rgba32> image, int maxCellWidth, int maxCellHeight) {
        ImageSize imageSize = SizeHelper.GetKittyTargetSize(image, maxCellWidth, maxCellHeight);
        Image<Rgba32> resizedImage = Resizer.ResizeForKitty(image, imageSize);
        // convert the resized image to base64
        using MemoryStream? ms = new();
        resizedImage.SaveAsPng(ms);
        byte[] imageBytes = ms.ToArray();
        string base64Image = Convert.ToBase64String(imageBytes);
        return (imageSize, ConvertToKittyGraphics(base64Image, imageSize));
    }
    private static string ConvertToKittyGraphics(string base64Image) {
        // basic implementation of kitty graphics protocol
        StringBuilder sb = new();
        int pos = 0;
        while (pos < base64Image.Length) {
            bool isFirstChunk = pos == 0;
            _ = sb.Append(Constants.KittyStart);
            if (isFirstChunk) {
                _ = sb.Append(Constants.KittyPos);
            }
            int remaining = base64Image.Length - pos;
            int chunkSize = Math.Min(Constants.KittychunkSize, remaining);
            ReadOnlySpan<char> chunk = base64Image.AsSpan(pos, chunkSize);
            pos += chunkSize;
            string chunkFlag = pos < base64Image.Length ? Constants.KittyMore : Constants.KittyFinish;
            if (isFirstChunk) {
                _ = sb.Append(',');
            }
            _ = sb
            .Append(chunkFlag)
            .Append(Constants.Divider)
            .Append(chunk)
            .Append(Constants.ST);
        }

        return sb.ToString();
    }
    private static string ConvertToKittyGraphics(string base64Image, ImageSize imageSize) {
        // implementation with cell dimension parameters
        StringBuilder sb = new();
        int pos = 0;
        while (pos < base64Image.Length) {
            bool isFirstChunk = pos == 0;
            _ = sb.Append(Constants.KittyStart);
            if (isFirstChunk) {
                _ = sb.Append(Constants.KittyPos);
                // Add cell dimension parameters so terminal knows exact size
                _ = sb.AppendFormat(CultureInfo.InvariantCulture, ",c={0},r={1}", imageSize.Width, imageSize.Height);
            }
            int remaining = base64Image.Length - pos;
            int chunkSize = Math.Min(Constants.KittychunkSize, remaining);
            ReadOnlySpan<char> chunk = base64Image.AsSpan(pos, chunkSize);
            pos += chunkSize;
            string chunkFlag = pos < base64Image.Length ? Constants.KittyMore : Constants.KittyFinish;
            if (isFirstChunk) {
                _ = sb.Append(',');
            }
            _ = sb
            .Append(chunkFlag)
            .Append(Constants.Divider)
            .Append(chunk)
            .Append(Constants.ST);
        }

        return sb.ToString();
    }
}
