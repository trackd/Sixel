using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sixel.Protocols;

public static class Blocks {
    public static (ImageSize Size, string Data) ImageToBlocks(Image<Rgba32> image, int maxCellWidth, int maxCellHeight) {
        ImageSize imageSize = SizeHelper.GetBlocksTargetSize(image, maxCellWidth, maxCellHeight);
        Image<Rgba32> resizedImage = Resizer.ResizeForBlocks(image, imageSize);
        ImageFrame<Rgba32> targetFrame = resizedImage.Frames[0];
        return (imageSize, ProcessFrameBlocks(targetFrame));
    }
    internal static string ProcessFrameBlocks(ImageFrame<Rgba32> frame) {
        var _buffer = new StringBuilder();
        for (int y = 0; y < frame.Height; y += 2) {
            for (int x = 0; x < frame.Width; x++) {
                Rgba32 topPixel = frame[x, y];
                Rgba32 bottomPixel = y + 1 < frame.Height ? frame[x, y + 1] : new Rgba32(0, 0, 0, 0);

                _buffer.ProcessPixelPairs(topPixel, bottomPixel);
            }
            _ = _buffer.AppendLine();
        }
        return _buffer.ToString();
    }
    private static void ProcessPixelPairs(this StringBuilder _buffer, Rgba32 top, Rgba32 bottom) {
        bool topTransparent = IsTransparent(top);
        bool bottomTransparent = IsTransparent(bottom);

        if (topTransparent && bottomTransparent) {
            _ = _buffer.Append(' ');
        }
        else if (topTransparent) {
            _buffer.AppendTopTransparent(bottom.R, bottom.G, bottom.B);
        }
        else if (bottomTransparent) {
            _buffer.AppendBottomTransparent(top.R, top.G, top.B);
        }
        else {
            _buffer.AppendBlock(top.R, top.G, top.B, bottom.R, bottom.G, bottom.B);
        }
    }
    private static void AppendTopTransparent(this StringBuilder Builder, byte r, byte g, byte b) {
        // "`e[38;2;{r};{g};{b}m▄`e[0m"
        _ = Builder.
        Append(Constants.ESC).
        Append(Constants.VTFG).
        Append(r).Append(';').
        Append(g).Append(';').
        Append(b).Append('m').
        Append(Constants.LowerHalfBlock).
        Append(Constants.Reset);
    }
    private static void AppendBottomTransparent(this StringBuilder Builder, byte r, byte g, byte b) {
        // "`e[38;2;{r};{g};{b}m▀`e[0m"
        _ = Builder.
        Append(Constants.ESC).
        Append(Constants.VTFG).
        Append(r).Append(';').
        Append(g).Append(';').
        Append(b).Append('m').
        Append(Constants.UpperHalfBlock).
        Append(Constants.Reset);
    }
    private static void AppendBlock(this StringBuilder Builder, byte tr, byte tg, byte tb, byte br, byte bg, byte bb) {
        // "`e[38;2;{tr};{tg};{tb};48;2;{br};{bg};{bb}m▀`e[0m"
        _ = Builder.
        Append(Constants.ESC).
        Append(Constants.VTFG).
        Append(tr).Append(';').
        Append(tg).Append(';').
        Append(tb).Append(';').
        Append(48).Append(';').
        Append(2).Append(';').
        Append(br).Append(';').
        Append(bg).Append(';').
        Append(bb).Append('m').
        Append(Constants.UpperHalfBlock).
        Append(Constants.Reset);
    }

    private static (byte R, byte G, byte B) BlendPixels(Rgba32 top, Rgba32 bottom) {
        // If pixel is fully transparent, return the background color
        if (IsTransparent(top) && IsTransparent(bottom)) {
            return (0, 0, 0);
        }

        float amount = top.A / 255f;

        byte r = (byte)((top.R * amount) + (bottom.R * (1 - amount)));
        byte g = (byte)((top.G * amount) + (bottom.G * (1 - amount)));
        byte b = (byte)((top.B * amount) + (bottom.B * (1 - amount)));

        return (r, g, b);
    }
    private static bool IsTransparent(Rgba32 pixel) {
        if (pixel.A < 8) return true;
        // if (pixel.A == 0) return true;
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
}
