#if NET472
using System.Drawing;
using System.Text;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;

internal sealed partial class ImageBackend {
    internal (ImageSize Size, string Data) Text(ImageSize cells, bool braille) => (cells, TextImage(image, cells, braille));
    private static string TextImage(Image image, ImageSize size, bool braille) {
        int columns = braille ? 2 : 1, rows = braille ? 4 : 2;
        int targetWidth = checked(size.Width * columns), targetHeight = checked(size.Height * rows);
        double scale = Math.Min((double)targetWidth / image.Width, (double)targetHeight / image.Height);
        using var fitted = DrawingRaster.Resize(image, Math.Max(1, (int)Math.Floor(image.Width * scale)), Math.Max(1, (int)Math.Floor(image.Height * scale)));
        using var bitmap = DrawingRaster.Pad(fitted, targetWidth, targetHeight);
        var raster = new DrawingRaster(bitmap);
        var result = new StringBuilder();
        for (int y = 0; y < bitmap.Height; y += rows) {
            for (int x = 0; x < bitmap.Width; x += columns) {
                if (!braille) {
                    Color top = Color.FromArgb(raster.Pixels[y * bitmap.Width + x]), bottom = Color.FromArgb(raster.Pixels[(y + 1) * bitmap.Width + x]);
                    bool topTransparent = IsTransparent(top), bottomTransparent = IsTransparent(bottom);
                    if (topTransparent && bottomTransparent) result.Append(' ');
                    else {
                        Color foreground = topTransparent ? bottom : top;
                        result.Append($"\x1b[38;2;{foreground.R};{foreground.G};{foreground.B}m");
                        if (!topTransparent && !bottomTransparent) result.Append($"\x1b[48;2;{bottom.R};{bottom.G};{bottom.B}m");
                        result.Append(topTransparent ? '▄' : '▀').Append("\x1b[0m");
                    }
                    continue;
                }
                int mask = 0, red = 0, green = 0, blue = 0, count = 0;
                int[] bits = [0, 1, 2, 6, 3, 4, 5, 7];
                for (int dx = 0; dx < 2; dx++) {
                    for (int dy = 0; dy < 4; dy++) {
                        Color pixel = Color.FromArgb(raster.Pixels[(y + dy) * bitmap.Width + x + dx]);
                        if (IsTransparent(pixel)) continue;
                        mask |= 1 << bits[dx * 4 + dy];
                        red += pixel.R; green += pixel.G; blue += pixel.B; count++;
                    }
                }
                if (count == 0) result.Append(' ');
                else result.Append($"\x1b[38;2;{red / count};{green / count};{blue / count}m").Append((char)(0x2800 + mask)).Append("\x1b[0m");
            }
            result.AppendLine();
        }
        return result.ToString();
    }

    private static bool IsTransparent(Color pixel) {
        if (pixel.A <= 8) return true;
        float luminance = ((0.299f * pixel.R) + (0.587f * pixel.G) + (0.114f * pixel.B)) / 255f;
        return (pixel.A < 32 && luminance < 0.15f) ||
            (pixel.A < 64 && pixel.R < 12 && pixel.G < 12 && pixel.B < 12) ||
            (pixel.A < 128 && luminance < 0.05f) || (pixel.A < 240 && luminance < 0.01f);
    }
}
#endif

