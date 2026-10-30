using System.Globalization;
using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

namespace Sixel.Protocols;

internal sealed class KittyImageOptions {
    internal int Width { get; set; }
    internal int Height { get; set; }
    internal int Format { get; set; } = 100;
    internal uint ImageId { get; set; }
    internal uint ImageNumber { get; set; }
    internal uint PlacementId { get; set; }
    internal bool Compress { get; set; }
    internal bool Animate { get; set; }
    internal bool Tmux { get; set; }
    internal int ChunkSize { get; set; } = 4096;
    internal bool Placeholder { get; set; }
    internal bool NoCursorMove { get; set; }
    internal bool ExplicitCursorMove { get; set; }
    internal char Action { get; set; } = 'T';
    internal int Quiet { get; set; } = 2;
    internal CellSize Cell { get; set; } = new() { PixelWidth = 10, PixelHeight = 20 };
    internal string[] Control { get; set; } = [];
}

internal static class KittyImage {
    internal static (string Text, ImageSize Cells, Size Pixels) Encode(Stream source, KittyImageOptions options) {
#if NET472
        using var image = Image.Load<Rgba32>(source, out var decodedFormat);
#else
        using var image = Image.Load<Rgba32>(source);
        var decodedFormat = image.Metadata.DecodedImageFormat;
#endif
        if (options.Animate && (options.ImageId == 0 || options.ImageNumber != 0 || options.Action != 'T'))
            throw new ArgumentException("Animation requires -ImageId and the TransmitAndDisplay action.", nameof(options));
        string animationFormat = decodedFormat?.Name.ToUpperInvariant() ?? string.Empty;
#if NET472
        bool supportedAnimation = animationFormat == "GIF";
#else
        bool supportedAnimation = animationFormat is "GIF" or "PNG" or "WEBP";
#endif
        if (options.Animate && !supportedAnimation)
            throw new ArgumentException("Automatic animation accepts GIF, plus animated PNG/WebP on PowerShell 7. Use Frame commands for other sources.", nameof(source));
        if (options.Placeholder && (options.ImageId == 0 || options.ImageNumber != 0 || options.Action != 'T' || options.NoCursorMove))
            throw new ArgumentException("Placeholders require -ImageId, TransmitAndDisplay, and normal cursor movement.", nameof(options));
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height, options.Width, options.Height, options.Cell, sixel: false);
        Resizer.ResizeToPixels(image, layout.Pixels, 0);
        var controls = new List<string> { "a=" + options.Action, Number('q', options.Quiet), Number('f', options.Format), "t=d" };
        if (options.ImageId != 0) controls.Add(Number('i', options.ImageId));
        if (options.ImageNumber != 0) controls.Add(Number('I', options.ImageNumber));
        if (options.PlacementId != 0) controls.Add(Number('p', options.PlacementId));
        if (options.Compress) controls.Add("o=z");
        bool explicitCursor = options.ExplicitCursorMove && options.Action == 'T' &&
            !options.Placeholder && !options.NoCursorMove && options.Control.Length == 0;
        if (options.NoCursorMove || explicitCursor) controls.Add("C=1");
        // Native pixel placement avoids stretching the last partial cell. c/r are
        // only required for a virtual placement, which preserves aspect ratio.
        if (options.Placeholder) {
            controls.AddRange(["U=1", Number('c', layout.Cells.Width), Number('r', layout.Cells.Height)]);
        }
        controls.AddRange(options.Control);
        var result = new StringBuilder();
        int firstFrame = 0;
#if !NET472
        if (options.Animate && animationFormat == "PNG" && !image.Metadata.GetPngMetadata().AnimateRootFrame && image.Frames.Count > 1)
            firstFrame = 1;
#endif
        using (Image<Rgba32> root = image.Frames.CloneFrame(firstFrame))
            result.Append(EncodeFrame(root, controls, options));
        if (options.Animate && image.Frames.Count - firstFrame > 1) {
            string id = Number('i', options.ImageId);
            string quiet = Number('q', options.Quiet);
            result.Append(KittyProtocol.Encode(["a=a", id, quiet, "s=1", "r=1", Number('z', FrameDelay(image, firstFrame, animationFormat))], tmux: options.Tmux));
            for (int index = firstFrame + 1; index < image.Frames.Count; index++) {
                using Image<Rgba32> frame = image.Frames.CloneFrame(index);
                var frameControls = new List<string> { "a=f", id, quiet, "t=d", Number('f', options.Format), "X=1", Number('z', FrameDelay(image, index, animationFormat)) };
                if (options.Compress) frameControls.Add("o=z");
                result.Append(EncodeFrame(frame, frameControls, options));
            }
            uint repeat = RepeatCount(image, animationFormat);
            result.Append(KittyProtocol.Encode(["a=a", id, quiet, "s=3", Number('v', repeat == 0 ? 1 : checked((long)repeat + 1))], tmux: options.Tmux));
        }
        if (options.Placeholder)
            result.Append(KittyPlaceholder.Encode(options.ImageId, options.PlacementId, layout.Cells.Width, layout.Cells.Height));
        if (explicitCursor)
            result.Append(CursorRows(layout.Cells.Height));
        return (result.ToString(), layout.Cells, layout.Pixels);
    }

    // ConPTY cannot observe cursor movement performed inside a graphics APC.
    // Leave the cursor on the last image row; PowerShell supplies the final newline.
    internal static string CursorRows(int rows) => string.Concat(Enumerable.Repeat("\r\n", Math.Max(0, rows - 1))) + "\r";

    private static uint RepeatCount(Image<Rgba32> image, string format) {
#if !NET472
        if (format == "PNG") return image.Metadata.GetPngMetadata().RepeatCount;
        if (format == "WEBP") return image.Metadata.GetWebpMetadata().RepeatCount;
#endif
        return image.Metadata.GetGifMetadata().RepeatCount;
    }

    private static int FrameDelay(Image<Rgba32> image, int index, string format) {
        int delay;
#if !NET472
        if (format == "PNG") delay = checked((int)Math.Round(image.Frames[index].Metadata.GetPngMetadata().FrameDelay.ToDouble() * 1000, MidpointRounding.AwayFromZero));
        else if (format == "WEBP") delay = checked((int)image.Frames[index].Metadata.GetWebpMetadata().FrameDelay);
        else
#endif
            delay = checked(image.Frames[index].Metadata.GetGifMetadata().FrameDelay * 10);
        return delay == 0 ? -1 : delay;
    }

    private static string EncodeFrame(Image<Rgba32> image, List<string> controls, KittyImageOptions options) {
        byte[] data;
        if (options.Format == 100) {
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            data = stream.ToArray();
        }
        else {
            int channels = options.Format == 24 ? 3 : 4;
            data = new byte[checked(image.Width * image.Height * channels)];
            int offset = 0;
            image.ProcessPixelRows(accessor => {
                for (int row = 0; row < accessor.Height; row++) {
                    foreach (Rgba32 pixel in accessor.GetRowSpan(row)) {
                        data[offset++] = pixel.R;
                        data[offset++] = pixel.G;
                        data[offset++] = pixel.B;
                        if (channels == 4) data[offset++] = pixel.A;
                    }
                }
            });
            controls.Add(Number('s', image.Width));
            controls.Add(Number('v', image.Height));
        }
        return KittyProtocol.Encode(controls, data, options.Tmux, options.ChunkSize);
    }

    internal static string Number(char key, long value) => key + "=" + value.ToString(CultureInfo.InvariantCulture);
}
