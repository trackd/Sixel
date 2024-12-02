using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Protocols;

public static class HalfBlock
{
    private static Rgba32 _backgroundColor;
    public static string ImageToAscii(Image<Rgba32> image, int cellWidth, int frame = 0, bool returnCursorToTopLeft = false)
    {
        _backgroundColor = GetConsoleBackgroundColor();
        var cellSize = Compatibility.GetCellSize();
        var maxWidth = Console.WindowWidth - 2;
        var characterWidth = (int)Math.Ceiling((double)image.Width / cellSize.PixelWidth);
        var characterHeight = (int)Math.Ceiling((double)image.Height / cellSize.PixelHeight);

        if (cellWidth <= 0 || cellWidth > maxWidth)
        {
            cellWidth = Math.Min(characterWidth, maxWidth);
        }
        image.Mutate(ctx =>
        {
            // Calculate target size in pixels based on character dimensions
            var pixelHeight = (int)Math.Round((double)image.Height / image.Width * cellWidth);
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(cellWidth, pixelHeight),
                Sampler = KnownResamplers.Bicubic,
                PremultiplyAlpha = false
            });
        });
        var size = new Size(image.Width, image.Height);
        var targetFrame = image.Frames[frame];
        return ProcessFrame(targetFrame, size, returnCursorToTopLeft);
    }
    private static string ProcessFrame(ImageFrame<Rgba32> frame, Size size, bool returnCursorToTopLeft)
    {
        var _buffer = new StringBuilder();

        for (int y = 0; y < size.Height; y += 2)
        {
            if (y + 1 >= size.Height)
            {
                _buffer.AppendLine();
                break;
            }

            for (int x = 0; x < size.Width; x++)
            {
                var topPixel = frame[x, y];
                var bottomPixel = frame[x, y + 1];

                _buffer.ProcessPixelPairs(topPixel, bottomPixel);
            }
            _buffer.AppendLine();
        }
        if (returnCursorToTopLeft)
        {

            // Move the cursor back to the top left of the image.
            _buffer.Append($"{Constants.ESC}[{size.Height}A");
        }
        return _buffer.ToString();
    }
    private static void ProcessPixelPairs(this StringBuilder _buffer, Rgba32 top, Rgba32 bottom)
    {
        bool topTransparent = IsTransparent(top);
        bool bottomTransparent = IsTransparent(bottom);

        if (topTransparent && bottomTransparent)
        {
            // Both pixels are transparent
            _buffer.Append(' ');
        }
        else if (topTransparent)
        {
            // Only bottom pixel is opaque, use lower half block
            var bottomRgb = BlendPixels(bottom);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{bottomRgb.R};{bottomRgb.G};{bottomRgb.B}m{Constants.LowerHalfBlock}{Constants.ESC}[0m");
        }
        else if (bottomTransparent)
        {
            // Only top pixel is opaque, use upper half block
            var topRgb = BlendPixels(top);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{topRgb.R};{topRgb.G};{topRgb.B}m{Constants.UpperHalfBlock}{Constants.ESC}[0m");
        }
        else
        {
            // Both pixels are opaque, set foreground and background colors, use upper half block
            var topRgb = BlendPixels(top);
            var bottomRgb = BlendPixels(bottom);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{topRgb.R};{topRgb.G};{topRgb.B}m");
            _buffer.Append($"{Constants.ESC}{Constants.VTBG}{bottomRgb.R};{bottomRgb.G};{bottomRgb.B}m{Constants.UpperHalfBlock}{Constants.ESC}[0m");
        }
    }

    private static (byte R, byte G, byte B) BlendPixels(Rgba32 pixel)
    {
        // fast path for fully transparent pixels
        if (pixel.A == 0)
        {
            return (pixel.R, pixel.G, pixel.B);
        }
        // var foregroundMultiplier = pixel.A / 255;
        // var backgroundMultiplier = 100 - foregroundMultiplier;
        // (byte)(pixel.R * foregroundMultiplier + _backgroundColor.R * backgroundMultiplier);
        float amount = pixel.A / 255f;

        byte r = (byte)(pixel.R * amount + _backgroundColor.R * (1 - amount));
        byte g = (byte)(pixel.G * amount + _backgroundColor.G * (1 - amount));
        byte b = (byte)(pixel.B * amount + _backgroundColor.B * (1 - amount));

        return (r, g, b);
    }
    private static bool IsTransparent(Rgba32 pixel) => pixel.A < 5;

    private static Rgba32 GetConsoleBackgroundColor()
    {
        var color = Console.BackgroundColor switch
        {
            ConsoleColor.Black => Color.FromRgb(0, 0, 0),
            ConsoleColor.Blue => Color.FromRgb(0, 0, 170),
            ConsoleColor.Cyan => Color.FromRgb(0, 170, 170),
            ConsoleColor.DarkBlue => Color.FromRgb(0, 0, 85),
            ConsoleColor.DarkCyan => Color.FromRgb(0, 85, 85),
            ConsoleColor.DarkGray => Color.FromRgb(85, 85, 85),
            ConsoleColor.DarkGreen => Color.FromRgb(0, 85, 0),
            ConsoleColor.DarkMagenta => Color.FromRgb(85, 0, 85),
            ConsoleColor.DarkRed => Color.FromRgb(85, 0, 0),
            ConsoleColor.DarkYellow => Color.FromRgb(85, 85, 0),
            ConsoleColor.Gray => Color.FromRgb(170, 170, 170),
            ConsoleColor.Green => Color.FromRgb(0, 170, 0),
            ConsoleColor.Magenta => Color.FromRgb(170, 0, 170),
            ConsoleColor.Red => Color.FromRgb(170, 0, 0),
            ConsoleColor.White => Color.FromRgb(255, 255, 255),
            ConsoleColor.Yellow => Color.FromRgb(170, 170, 0),
            _ => Color.FromRgb(0, 0, 0)
        };
        return color.ToPixel<Rgba32>();
    }
}
