using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sixel.Protocols;

public static class Blocks
{
    public static string ImageToBlocks(Image<Rgba32> image, ImageSize imageSize)
    {
        var resizedImage = Resizer.ResizeToCharacterCells(image, imageSize, 0, false);
        var targetFrame = resizedImage.Frames[0];
        return ProcessFrame(targetFrame);
    }
    internal static string ProcessFrame(ImageFrame<Rgba32> frame)
    {
        var _buffer = new StringBuilder();
        var _backgroundColor = GetConsoleBackgroundColor();

        for (int y = 0; y < frame.Height; y += 2)
        {
            if (y + 1 >= frame.Height)
            {
                _buffer.AppendLine();
                break;
            }

            for (int x = 0; x < frame.Width; x++)
            {
                var topPixel = frame[x, y];
                var bottomPixel = frame[x, y + 1];

                _buffer.ProcessPixelPairs(topPixel, bottomPixel, _backgroundColor);
            }
            _buffer.AppendLine();
        }
        return _buffer.ToString();
    }
    private static void ProcessPixelPairs(this StringBuilder _buffer, Rgba32 top, Rgba32 bottom, Rgba32 _backgroundColor)
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
            var bottomRgb = BlendPixels(bottom, _backgroundColor);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{bottomRgb.R};{bottomRgb.G};{bottomRgb.B}m{Constants.LowerHalfBlock}{Constants.ESC}[0m".AsSpan());
        }
        else if (bottomTransparent)
        {
            // Only top pixel is opaque, use upper half block
            var topRgb = BlendPixels(top, _backgroundColor);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{topRgb.R};{topRgb.G};{topRgb.B}m{Constants.UpperHalfBlock}{Constants.ESC}[0m".AsSpan());
        }
        else
        {
            // Both pixels are opaque, set foreground and background colors, use upper half block
            var topRgb = BlendPixels(top, _backgroundColor);
            var bottomRgb = BlendPixels(bottom, _backgroundColor);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{topRgb.R};{topRgb.G};{topRgb.B}m".AsSpan());
            _buffer.Append($"{Constants.ESC}{Constants.VTBG}{bottomRgb.R};{bottomRgb.G};{bottomRgb.B}m{Constants.UpperHalfBlock}{Constants.ESC}[0m".AsSpan());
        }
    }

    private static (byte R, byte G, byte B) BlendPixels(Rgba32 pixel, Rgba32 _backgroundColor)
    {
        // If no background color is provided, use the console background color
        // if (_backgroundColor == default)
        // {
        //     _backgroundColor = GetConsoleBackgroundColor();
        // }
        // fast path for fully transparent pixels
        if (pixel.A == 0)
        {
            return (pixel.R, pixel.G, pixel.B);
        }
        // If pixel is fully transparent, return the background color
        // if (IsTransparent(pixel))
        // {
        //     return (_backgroundColor.R, _backgroundColor.G, _backgroundColor.B);
        // }

        float amount = pixel.A / 255f;

        byte r = (byte)(pixel.R * amount + (_backgroundColor.R * (1 - amount)));
        byte g = (byte)(pixel.G * amount + (_backgroundColor.G * (1 - amount)));
        byte b = (byte)(pixel.B * amount + (_backgroundColor.B * (1 - amount)));

        return (r, g, b);
    }
    private static bool IsTransparent(Rgba32 pixel) => pixel.A < 5;
    private static Rgba32 GetConsoleBackgroundColor()
    {
        var color = Console.BackgroundColor switch {
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
            _ => Color.Transparent,
        };
        return color.ToPixel<Rgba32>();
    }

}
