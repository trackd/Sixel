#if !NET472
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;

// Only image operations live here. Protocol framing and placement are shared.
internal sealed class ImageBackend : IDisposable {
    private readonly Image<Rgba32> image;
    private readonly string format;

    private ImageBackend(Image<Rgba32> image) {
        this.image = image;
        format = image.Metadata.DecodedImageFormat?.Name.ToUpperInvariant() ?? string.Empty;
    }

    internal static ImageBackend Load(Stream source) => new(Image.Load<Rgba32>(source));
    internal int Width => image.Width;
    internal int Height => image.Height;
    internal int FrameCount => image.Frames.Count;
    internal bool SupportsAnimation => format is "GIF" or "PNG" or "WEBP";
    internal int FirstAnimationFrame => format == "PNG" && !image.Metadata.GetPngMetadata().AnimateRootFrame && FrameCount > 1 ? 1 : 0;
    internal uint RepeatCount => format switch {
        "PNG" => image.Metadata.GetPngMetadata().RepeatCount,
        "WEBP" => image.Metadata.GetWebpMetadata().RepeatCount,
        _ => image.Metadata.GetGifMetadata().RepeatCount
    };

    internal int FrameDelay(int index) => format switch {
        "PNG" => checked((int)Math.Round(image.Frames[index].Metadata.GetPngMetadata().FrameDelay.ToDouble() * 1000, MidpointRounding.AwayFromZero)),
        "WEBP" => checked((int)image.Frames[index].Metadata.GetWebpMetadata().FrameDelay),
        _ => GifDelay(index)
    };
    internal int GifDelay(int index) => checked(image.Frames[index].Metadata.GetGifMetadata().FrameDelay * 10);

    internal void Resize(int width, int height, int maxColors) => Resizer.ResizeToPixels(image, new Size(width, height), maxColors);
    internal void Pad(ImageSize cells, CellSize cell) => Resizer.PadToCells(image, cells, cell);
    internal string SixelFrame(int index) => Protocols.Sixel.FrameToSixelString(image.Frames[index]);
    internal Image<Rgba32> NativeImage => image;
    internal (ImageSize Size, string Data) Text(ImageSize cells, bool braille) => braille
        ? Protocols.Braille.ImageToBraille(image, cells.Width, cells.Height)
        : (cells, Protocols.Blocks.ImageToBlocks(image, cells));

    internal byte[] InlineBytes() {
        using var stream = new MemoryStream();
        if (FrameCount > 1) image.SaveAsGif(stream);
        else image.SaveAsPng(stream);
        return stream.ToArray();
    }

    internal byte[] FrameBytes(int index, int pixelFormat) {
        if (pixelFormat == 100) {
            using var stream = new MemoryStream();
            using Image<Rgba32> singleFrame = image.Frames.CloneFrame(index);
            singleFrame.SaveAsPng(stream);
            return stream.ToArray();
        }
        ImageFrame<Rgba32> frame = image.Frames[index];
        int channels = pixelFormat == 24 ? 3 : 4;
        var data = new byte[checked(frame.Width * frame.Height * channels)];
        if (channels == 4) {
            frame.CopyPixelDataTo(data);
            return data;
        }
        int offset = 0;
        frame.ProcessPixelRows(accessor => {
            for (int row = 0; row < accessor.Height; row++) {
                foreach (Rgba32 pixel in accessor.GetRowSpan(row)) {
                    data[offset++] = pixel.R;
                    data[offset++] = pixel.G;
                    data[offset++] = pixel.B;
                }
            }
        });
        return data;
    }

    public void Dispose() => image.Dispose();
}
#endif
