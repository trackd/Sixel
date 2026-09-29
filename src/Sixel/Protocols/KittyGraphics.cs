using System.Text;
using System.Globalization;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Protocols;

public static class KittyGraphics {
    /// <summary>
    /// Converts an image stream to Kitty Graphics Protocol format.
    /// </summary>
    /// <returns>The Kitty Graphics Protocol formatted string.</returns>
    public static string ImageToKitty(Stream imageStream) {
        if (imageStream.CanSeek)
            imageStream.Position = 0;
        using var image = Image.Load<Rgba32>(imageStream);
        CellSize cell = Compatibility.GetCellSize();
        ImageSize cells = ImageLayout.Measure(image.Width, image.Height, cell, sixel: false);
        Resizer.PadToCells(image, cells, cell);
        return Encode(image, cells);
    }
    public static string ImageToKitty(Image<Rgba32> image, ImageSize imageSize) {
        CellSize cell = Compatibility.GetCellSize();
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height,
            imageSize.Width, imageSize.Height, cell, sixel: false);
        Resizer.ResizeToPixels(image, layout.Pixels, 0);
        Resizer.PadToCells(image, layout.Cells, cell);
        return Encode(image, layout.Cells);
    }

    internal static string Encode(Image<Rgba32> image, ImageSize imageSize) {
        using MemoryStream ms = new();
        image.SaveAsPng(ms);
        byte[] imageBytes = ms.ToArray();
        string base64Image = Convert.ToBase64String(imageBytes);
        return ConvertToKittyGraphics(base64Image, imageSize);
    }
    private static string ConvertToKittyGraphics(string base64Image, ImageSize imageSize) {
        // basic implementation of kitty graphics protocol
        StringBuilder sb = new();
        int pos = 0;
        while (pos < base64Image.Length) {
            _ = sb.Append(Constants.KittyStart);
            if (pos == 0) {
                _ = sb.Append(Constants.KittyPos)
                    .Append("t=d,q=2,c=").Append(imageSize.Width.ToString(CultureInfo.InvariantCulture))
                    .Append(",r=").Append(imageSize.Height.ToString(CultureInfo.InvariantCulture)).Append(',');
            }
            else {
                sb.Append("q=2,");
            }
            int remaining = base64Image.Length - pos;
            string chunk = base64Image.Substring(pos, Math.Min(Constants.KittychunkSize, remaining));
            pos += chunk.Length;
            _ = pos < base64Image.Length ? sb.Append(Constants.KittyMore) : sb.Append(Constants.KittyFinish);
            _ = sb.Append(Constants.Divider)
                .Append(chunk)
                .Append(Constants.ST);
        }

        return sb.ToString();
    }
}
