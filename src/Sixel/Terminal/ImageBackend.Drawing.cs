#if NET472
using System.Drawing;
using System.Drawing.Imaging;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;

internal sealed partial class ImageBackend : IDisposable {
    private readonly Image image;
    private int width;
    private int height;
    private int maxColors;
    private int paddedWidth;
    private int paddedHeight;
    // Per-image workspace: GIF frames reuse the bounded histogram, never palette
    // colors or diffusion errors. No global state or rented buffers outlive a call.
    private DrawingQuantizer? quantizer;

    private ImageBackend(Image image) {
        this.image = image;
        width = image.Width;
        height = image.Height;
    }

    internal static ImageBackend Load(Stream source) => new(Image.FromStream(source));
    internal int Width => paddedWidth == 0 ? width : paddedWidth;
    internal int Height => paddedHeight == 0 ? height : paddedHeight;
    internal int FrameCount => DrawingRaster.FrameCount(image);
    internal bool SupportsAnimation => image.RawFormat.Guid == ImageFormat.Gif.Guid;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static",
        Justification = "The ImageSharp implementation reads APNG metadata through the same instance contract.")]
    internal int FirstAnimationFrame => 0;
    internal uint RepeatCount => DrawingRaster.RepeatCount(image);
    internal int FrameDelay(int index) => DrawingRaster.FrameDelay(image, index);
    internal int GifDelay(int index) => FrameDelay(index);

    // GDI+ selects frames on the decoder. Apply the same transform to each selected
    // frame lazily, keeping only one independent bitmap alive at a time.
    internal void Resize(int width, int height, int maxColors) {
        this.width = width;
        this.height = height;
        this.maxColors = maxColors;
    }

    internal void Pad(ImageSize cells, CellSize cell) {
        paddedWidth = checked(cells.Width * cell.PixelWidth);
        paddedHeight = checked(cells.Height * cell.PixelHeight);
    }

    private Bitmap RenderFrame(int index) {
        DrawingRaster.SelectFrame(image, index);
        Bitmap frame = DrawingRaster.Resize(image, width, height);
        try {
            if (paddedWidth == 0) return frame;
            Bitmap padded = DrawingRaster.Pad(frame, paddedWidth, paddedHeight);
            frame.Dispose();
            return padded;
        }
        catch { frame.Dispose(); throw; }
    }

    internal string SixelFrame(int index) {
        using var frame = RenderFrame(index);
        var raster = new DrawingRaster(frame);
        quantizer ??= new DrawingQuantizer();
        quantizer.Quantize(raster.Pixels.AsSpan(), raster.Width, maxColors);
        return Protocols.Sixel.FrameToSixelString(raster);
    }

    internal byte[] InlineBytes() {
        if (FrameCount > 1)
            throw new NotSupportedException("Resized inline animation requires PowerShell 7. Use ConvertTo-SixelGif or ConvertTo-Kitty -Animate on Windows PowerShell.");
        return FrameBytes(0, 100);
    }

    internal byte[] FrameBytes(int index, int pixelFormat) {
        using var frame = RenderFrame(index);
        return pixelFormat == 100 ? DrawingRaster.Png(frame) : new DrawingRaster(frame).Bytes(pixelFormat == 24 ? 3 : 4);
    }

    public void Dispose() => image.Dispose();
}
#endif
