#if NET472
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Sixel.Terminal;

// GDI+ is confined to Windows PowerShell. The caller keeps the decoder stream
// alive until Image.Dispose; resized frames own independent 32-bit bitmaps.
internal sealed class DrawingRaster {
    internal int Width { get; }
    internal int Height { get; }
    internal int[] Pixels { get; }

    internal DrawingRaster(Bitmap bitmap) {
        Width = bitmap.Width;
        Height = bitmap.Height;
        Pixels = new int[checked(Width * Height)];
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try {
            for (int y = 0; y < Height; y++) {
                Marshal.Copy(IntPtr.Add(data.Scan0, checked(y * data.Stride)), Pixels, y * Width, Width);
            }
        }
        finally { bitmap.UnlockBits(data); }
    }

    internal byte[] Bytes(int channels) {
        var result = new byte[checked(Pixels.Length * channels)];
        int at = 0;
        foreach (int pixel in Pixels) {
            result[at++] = (byte)(pixel >> 16);
            result[at++] = (byte)(pixel >> 8);
            result[at++] = (byte)pixel;
            if (channels == 4) result[at++] = (byte)(pixel >> 24);
        }
        return result;
    }

    internal static Bitmap Resize(Image image, int width, int height) {
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try {
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(image, new Rectangle(0, 0, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    internal static Bitmap Pad(Bitmap image, int width, int height) {
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try {
            using var graphics = Graphics.FromImage(result);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(image, 0, 0);
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    internal static byte[] Png(Bitmap image) {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    internal static int FrameCount(Image image) => image.FrameDimensionsList.Contains(FrameDimension.Time.Guid)
        ? image.GetFrameCount(FrameDimension.Time) : 1;

    internal static void SelectFrame(Image image, int index) {
        if (image.FrameDimensionsList.Contains(FrameDimension.Time.Guid)) image.SelectActiveFrame(FrameDimension.Time, index);
    }

    internal static int FrameDelay(Image image, int index) {
        byte[]? delays = Property(image, 0x5100);
        return delays is not null && delays.Length >= checked((index + 1) * 4)
            ? checked((int)BitConverter.ToUInt32(delays, index * 4) * 10) : 100;
    }

    internal static uint RepeatCount(Image image) {
        byte[]? repeat = Property(image, 0x5101);
        return repeat is not null && repeat.Length >= 2 ? BitConverter.ToUInt16(repeat, 0) : 1u;
    }

    private static byte[]? Property(Image image, int id) => image.PropertyIdList.Contains(id) ? image.GetPropertyItem(id).Value : null;

    internal void Quantize(int maxColors) => new DrawingQuantizer().Quantize(Pixels.AsSpan(), Width, maxColors);
}
#endif
