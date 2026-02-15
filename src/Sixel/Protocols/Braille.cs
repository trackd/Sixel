
using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Protocols;

public static class Braille {
    public static (ImageSize Size, string Data) ImageToBraille(Image<Rgba32> image, int maxCellWidth, int maxCellHeight) {
        ImageSize imageSize = SizeHelper.GetBrailleTargetSize(image, maxCellWidth, maxCellHeight);
        Image<Rgba32> resizedImage = Resizer.ResizeForBraille(image, imageSize);
        ImageFrame<Rgba32> targetFrame = resizedImage.Frames[0];
        return (imageSize, ProcessFrameBraille(targetFrame));
    }
    private static string ProcessFrameBraille(ImageFrame<Rgba32> frame) {
        var _buffer = new StringBuilder();
        int width = frame.Width;
        int height = frame.Height;

        for (int y = 0; y < height; y += 4) {
            for (int x = 0; x < width; x += 2) {
                int dotBits = 0;
                int colorWeightSum = 0;
                int rSum = 0, gSum = 0, bSum = 0;

                for (int dx = 0; dx < 2; dx++) {
                    for (int dy = 0; dy < 4; dy++) {
                        int sampleX = x + dx;
                        int sampleY = y + dy;

                        if (sampleX >= width || sampleY >= height) {
                            continue;
                        }

                        Rgba32 px = frame[sampleX, sampleY];
                        bool on = !IsTransparentAdv(px);
                        if (on) {
                            int dotIndex = dx == 0 ? (dy == 0 ? 0 : dy == 1 ? 1 : dy == 2 ? 2 : 6) : (dy == 0 ? 3 : dy == 1 ? 4 : dy == 2 ? 5 : 7);
                            dotBits |= 1 << dotIndex;
                            int alphaWeight = px.A;
                            rSum += px.R * alphaWeight;
                            gSum += px.G * alphaWeight;
                            bSum += px.B * alphaWeight;
                            colorWeightSum += alphaWeight;
                        }
                    }
                }

                if (dotBits == 0) {
                    _ = _buffer.Append(' ');
                }
                else {
                    int safeWeight = Math.Max(1, colorWeightSum);
                    byte R = (byte)(rSum / safeWeight);
                    byte G = (byte)(gSum / safeWeight);
                    byte B = (byte)(bSum / safeWeight);
                    int codepoint = 0x2800 + dotBits;
                    _buffer.AppendCodepoint(codepoint, R, G, B);
                }
            }

            _ = _buffer.AppendLine();
        }

        return _buffer.ToString();
    }
    private static void AppendCodepoint(this StringBuilder Builder, int codepoint, byte r, byte g, byte b) {
        // "`e[38;2;{r};{g};{b}m{codepoint}`e[0m"
        _ = Builder.
        Append(Constants.ESC).
        Append(Constants.VTFG).
        Append(r).Append(';').
        Append(g).Append(';').
        Append(b).Append('m').
        Append((char)codepoint).
        Append(Constants.Reset);
    }
    private static bool IsTransparent(Rgba32 pixel) => pixel.A == 0;
    private static bool IsTransparentAdv(Rgba32 pixel) {
        if (pixel.A == 0) {
            return true;
        }

        float luminance = ((0.299f * pixel.R) + (0.587f * pixel.G) + (0.114f * pixel.B)) / 255f;
        return pixel.A < 8 ||
                (pixel.A < 32 && luminance < 0.15f) ||
                (pixel.A < 64 && pixel.R < 12 && pixel.G < 12 && pixel.B < 12) ||
                (pixel.A < 128 && luminance < 0.05f) ||
                (pixel.A < 240 && luminance < 0.01f);
    }
}
