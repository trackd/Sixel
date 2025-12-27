using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sixel.Protocols;

public static class Blocks {
    public static string ImageToBlocks(Image<Rgba32> image, ImageSize imageSize) {
        // Resize the image directly to character cell dimensions (not pixel dimensions)
        image.Mutate(ctx => {
            ctx.Resize(new ResizeOptions {
                Mode = ResizeMode.BoxPad,
                Position = AnchorPositionMode.TopLeft,
                PadColor = Color.Transparent,
                // * 2 because each cell is 2 pixels high for blocks
                Size = new Size(imageSize.Width, imageSize.Height * 2),
                Sampler = KnownResamplers.Bicubic, // Better for preserving sharp transparency edges
                PremultiplyAlpha = false
            });
        });
        ImageFrame<Rgba32> targetFrame = image.Frames[0];
        return ProcessFrame(targetFrame);
    }

    internal static string ProcessFrame(ImageFrame<Rgba32> frame) {
        var _buffer = new StringBuilder();
        Rgba32 _backgroundColor = GetConsoleBackgroundColor();

        for (int y = 0; y < frame.Height; y += 2) {
            if (y + 1 >= frame.Height) {
                _buffer.AppendLine();
                break;
            }

            for (int x = 0; x < frame.Width; x++) {
                Rgba32 topPixel = frame[x, y];
                Rgba32 bottomPixel = frame[x, y + 1];

                _buffer.ProcessPixelPairs(topPixel, bottomPixel, _backgroundColor);
            }
            _buffer.AppendLine();
        }
        return _buffer.ToString();
    }
    private static void ProcessPixelPairs(this StringBuilder _buffer, Rgba32 top, Rgba32 bottom, Rgba32 _backgroundColor) {
        bool topTransparent = IsTransparent(top);
        bool bottomTransparent = IsTransparent(bottom);

        if (topTransparent && bottomTransparent) {
            // Both pixels are transparent
            _buffer.Append(' ');
        }
        else if (topTransparent) {
            // Only bottom pixel is opaque, use lower half block
            (byte R, byte G, byte B) = BlendPixels(bottom, _backgroundColor);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{R};{G};{B}m{Constants.LowerHalfBlock}{Constants.ESC}[0m".AsSpan());
        }
        else if (bottomTransparent) {
            // Only top pixel is opaque, use upper half block
            (byte R, byte G, byte B) = BlendPixels(top, _backgroundColor);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{R};{G};{B}m{Constants.UpperHalfBlock}{Constants.ESC}[0m".AsSpan());
        }
        else {
            // Both pixels are opaque, set foreground and background colors, use upper half block
            (byte R, byte G, byte B) = BlendPixels(top, _backgroundColor);
            (byte R, byte G, byte B) bottomRgb = BlendPixels(bottom, _backgroundColor);
            _buffer.Append($"{Constants.ESC}{Constants.VTFG}{R};{G};{B}m".AsSpan());
            _buffer.Append($"{Constants.ESC}{Constants.VTBG}{bottomRgb.R};{bottomRgb.G};{bottomRgb.B}m{Constants.UpperHalfBlock}{Constants.ESC}[0m".AsSpan());
        }
    }
    private static (byte R, byte G, byte B) BlendPixels(Rgba32 pixel, Rgba32 _backgroundColor) {
        // If pixel is fully transparent, return the background color
        if (IsTransparent(pixel)) {
            return (_backgroundColor.R, _backgroundColor.G, _backgroundColor.B);
        }

        float amount = pixel.A / 255f;

        byte r = (byte)((pixel.R * amount) + (_backgroundColor.R * (1 - amount)));
        byte g = (byte)((pixel.G * amount) + (_backgroundColor.G * (1 - amount)));
        byte b = (byte)((pixel.B * amount) + (_backgroundColor.B * (1 - amount)));

        return (r, g, b);
    }
    private static bool IsTransparent(Rgba32 pixel) {
        // Calculate luminance for better edge artifact detection
        float luminance = ((0.299f * pixel.R) + (0.587f * pixel.G) + (0.114f * pixel.B)) / 255f;

        // Consider pixels transparent if:
        // 1. Alpha is very low (traditional transparency)
        // 2. Alpha is low and pixel is very dark (common resizing artifacts)
        // 3. Alpha is moderate and luminance is extremely low (aggressive edge artifact removal)
        // 4. Alpha is low and color is close to pure black (black edge artifacts)
        // 5. Very aggressive: moderately transparent with low luminance (catches most edge cases)
        return pixel.A < 8 ||
               (pixel.A < 32 && luminance < 0.15f) ||
               (pixel.A < 64 && pixel.R < 12 && pixel.G < 12 && pixel.B < 12) ||
               (pixel.A < 128 && luminance < 0.05f) ||
               (pixel.A < 240 && luminance < 0.01f);
    }
    private static Rgba32 GetConsoleBackgroundColor() {
        Color color = Console.BackgroundColor switch {
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
