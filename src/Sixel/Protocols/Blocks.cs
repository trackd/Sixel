using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sixel.Protocols;

public static class Blocks {
    private static readonly Rgba32 s_transparent = new(0, 0, 0, 0);
    public static string ImageToBlocks(Image<Rgba32> image, ImageSize imageSize) {
        int targetWidth = imageSize.Width;
        int targetHeight = imageSize.Height * 2;

        if (targetWidth <= 0 || targetHeight <= 0) {
            return string.Empty;
        }

        if (image.Width != targetWidth || image.Height != targetHeight) {
            // Resize directly to the block sampling grid.
            image.Mutate(ctx => {
                ctx.Resize(new ResizeOptions {
                    Mode = ResizeMode.BoxPad,
                    Position = AnchorPositionMode.TopLeft,
                    PadColor = Color.Transparent,
                    // *2 because each cell is 2 pixels high for blocks.
                    Size = new Size(targetWidth, targetHeight),
                    Sampler = KnownResamplers.Bicubic,
                    PremultiplyAlpha = false
                });
            });
        }

        ImageFrame<Rgba32> targetFrame = image.Frames[0];
        return ProcessFrameBlocks(targetFrame);
    }

    internal static string ProcessFrameBlocks(ImageFrame<Rgba32> frame) {
        var _buffer = new StringBuilder(frame.Width * frame.Height * 6);

        for (int y = 0; y < frame.Height; y += 2) {
            for (int x = 0; x < frame.Width; x++) {
                Rgba32 topPixel = frame[x, y];
                Rgba32 bottomPixel = y + 1 < frame.Height ? frame[x, y + 1] : s_transparent;

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
            _buffer.AppendSpace();
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
    private static void AppendSpace(this StringBuilder Builder) => Builder.Append(' ');

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

    // private static bool IsTransparent(Rgba32 pixel) => pixel.A == 0;
    private static bool IsTransparent(Rgba32 pixel) {
        if (pixel.A <= 8) {
            return true;
        }

        float luminance = ((0.299f * pixel.R) + (0.587f * pixel.G) + (0.114f * pixel.B)) / 255f;
        return (pixel.A < 32 && luminance < 0.15f) ||
                (pixel.A < 64 && pixel.R < 12 && pixel.G < 12 && pixel.B < 12) ||
                (pixel.A < 128 && luminance < 0.05f) ||
                (pixel.A < 240 && luminance < 0.01f);
    }
}
