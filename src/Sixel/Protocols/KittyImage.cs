using System.Globalization;
using Sixel.Terminal;
using Sixel.Terminal.Models;
#if NET472
using Size = System.Drawing.Size;
#else
using Size = SixLabors.ImageSharp.Size;
#endif

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
        using var image = ImageBackend.Load(source);

        if (options.Animate && (options.ImageId == 0 || options.ImageNumber != 0 || options.Action != 'T'))
            throw new ArgumentException("Animation requires -ImageId and the TransmitAndDisplay action.", nameof(options));
        if (options.Animate && !image.SupportsAnimation)
            throw new ArgumentException("Automatic animation accepts GIF, plus animated PNG/WebP on PowerShell 7. Use Frame commands for other sources.", nameof(source));
        if (options.Placeholder && (options.ImageId == 0 || options.ImageNumber != 0 || options.Action != 'T' || options.NoCursorMove))
            throw new ArgumentException("Placeholders require -ImageId, TransmitAndDisplay, and normal cursor movement.", nameof(options));
        ImageLayout layout = ImageLayout.Calculate(image.Width, image.Height, options.Width, options.Height, options.Cell, sixel: false);
        image.Resize(layout.Pixels.Width, layout.Pixels.Height, 0);
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
        using var result = new StringWriter(CultureInfo.InvariantCulture);
        int firstFrame = options.Animate ? image.FirstAnimationFrame : 0;
        WriteFrame(result, image, firstFrame, controls, options);
        if (options.Animate && image.FrameCount - firstFrame > 1) {
            string id = Number('i', options.ImageId);
            string quiet = Number('q', options.Quiet);
            KittyProtocol.WriteTo(result, ["a=a", id, quiet, "s=1", "r=1", Number('z', AnimationDelay(image, firstFrame))], tmux: options.Tmux);
            for (int index = firstFrame + 1; index < image.FrameCount; index++) {

                var frameControls = new List<string> { "a=f", id, quiet, "t=d", Number('f', options.Format), "X=1", Number('z', AnimationDelay(image, index)) };
                if (options.Compress) frameControls.Add("o=z");
                WriteFrame(result, image, index, frameControls, options);
            }
            uint repeat = image.RepeatCount;
            KittyProtocol.WriteTo(result, ["a=a", id, quiet, "s=3", Number('v', repeat == 0 ? 1 : checked((long)repeat + 1))], tmux: options.Tmux);
        }
        if (options.Placeholder)
            result.Write(KittyPlaceholder.Encode(options.ImageId, options.PlacementId, layout.Cells.Width, layout.Cells.Height));
        if (explicitCursor)
            result.Write(CursorRows(layout.Cells.Height));
        return (result.ToString(), layout.Cells, layout.Pixels);
    }

    // ConPTY cannot observe cursor movement performed inside a graphics APC.
    // Leave the cursor on the last image row; PowerShell supplies the final newline.
    internal static string CursorRows(int rows) => string.Concat(Enumerable.Repeat("\r\n", Math.Max(0, rows - 1))) + "\r";

    private static int AnimationDelay(ImageBackend image, int index) {
        int delay = image.FrameDelay(index);
        return delay == 0 ? -1 : delay;
    }

    private static void WriteFrame(TextWriter writer, ImageBackend image, int index, List<string> controls, KittyImageOptions options) {
        byte[] data = image.FrameBytes(index, options.Format);
        if (options.Format != 100) {
            controls.Add(Number('s', image.Width));
            controls.Add(Number('v', image.Height));
        }
        KittyProtocol.WriteTo(writer, controls, data, options.Tmux, options.ChunkSize);
    }
    internal static string Number(char key, long value) => key + "=" + value.ToString(CultureInfo.InvariantCulture);
}
