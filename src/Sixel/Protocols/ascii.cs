using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace Sixel.Protocols;

// ignore this, nothing to see here.. move along.
// not finished..
public static class HalfBlockCell
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
        image.Mutate(ctx => {
            // Calculate target size in pixels based on character dimensions
            var pixelHeight = (int)Math.Round((double)image.Height / image.Width * cellWidth);
            ctx.Resize(new ResizeOptions {
                Size = new Size(cellWidth, pixelHeight),
                Sampler = KnownResamplers.Bicubic,
                PremultiplyAlpha = false
            });
        });
        var size = new Size(image.Width, image.Height);
        var targetFrame = image.Frames[frame];
        return FrameToAsciiString(targetFrame, size, returnCursorToTopLeft);
    }
    private static string FrameToAsciiString(ImageFrame<Rgba32> frame, Size size, bool returnCursorToTopLeft)
    {
        var _buffer = new StringBuilder();
        frame.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y += 2)
            {
                if (y + 1 >= accessor.Height)
                {
                    _buffer.AppendLine();
                    break;
                }

                Span<Rgba32> topRow = accessor.GetRowSpan(y);
                Span<Rgba32> bottomRow = y + 1 < accessor.Height ? accessor.GetRowSpan(y + 1) : default;

                for (int i = 0; i < topRow.Length; i++)
                {
                    ref Rgba32 topPixel = ref topRow[i];
                    ref Rgba32 bottomPixel = ref bottomRow.IsEmpty ? ref topPixel : ref bottomRow[i];

                    _buffer.ProcessPixelPair(topPixel, bottomPixel);
                }
                _buffer.AppendLine();
            }
        });
        return _buffer.ToString().Trim();
    }
    private static void ProcessPixelPair(this StringBuilder _buffer, Rgba32 top, Rgba32 bottom)
    {
        var topRgb = BlendPixel(top);
        var bottomRgb = BlendPixel(bottom);
        if (IsTransparent(bottom))
        {
            _buffer.Append($"{Constants.ESC}[0m");
        }
        else
        {
            _buffer.Append($"{Constants.ESC}[38;2;{bottomRgb.R};{bottomRgb.G};{bottomRgb.B}m");
        }

        if (IsTransparent(top))
        {
            _buffer.Append($"{Constants.ESC}[0m ");
        }
        else
        {
            _buffer.Append($"{Constants.ESC}[48;2;{topRgb.R};{topRgb.G};{topRgb.B}m{Constants.LowerHalfBlock}{Constants.ESC}[0m");
        }


    }

    private static (byte R, byte G, byte B) BlendPixel(Rgba32 pixel)
    {
        if (pixel.A == 0)
        {
            return (pixel.R, pixel.G, pixel.B);
        }
        var foregroundMultiplier = pixel.A / 255;
        var backgroundMultiplier = 100 - foregroundMultiplier;
        //var foregroundMultiplier = pixel.A / 255f;
        //var backgroundMultiplier = 1.0f - foregroundMultiplier;
        //var backgroundMultiplier = (255 - pixel.A) / 255f;
        return (
            (byte)Math.Min(255, (pixel.R * foregroundMultiplier + _backgroundColor.R * backgroundMultiplier)),
            (byte)Math.Min(255, (pixel.G * foregroundMultiplier + _backgroundColor.G * backgroundMultiplier)),
            (byte)Math.Min(255, (pixel.B * foregroundMultiplier + _backgroundColor.B * backgroundMultiplier))
        );

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
            _ => Color.FromRgb(0, 0, 0)
        };
        return color.ToPixel<Rgba32>();
    }
}
