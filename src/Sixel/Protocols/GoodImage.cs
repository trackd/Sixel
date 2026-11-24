using Sixel.Terminal;
using Sixel.Terminal.Models;
#if NET472
using Size = System.Drawing.Size;
#else
using Size = SixLabors.ImageSharp.Size;
#endif

namespace Sixel.Protocols;

/// <summary>What ConvertTo-GoodImage does with a decoded source.</summary>
internal enum GoodImageAction {
    /// <summary>o=s, or o=u followed by o=r when the image is named.</summary>
    Display,
    /// <summary>o=u only, for later o=r renders by name.</summary>
    Upload,
}

internal sealed class GoodImageOptions {
    /// <summary>Requested cells (c/r). Zero leaves the axis to the aspect ratio.</summary>
    internal int Width { get; set; }
    internal int Height { get; set; }
    internal CellSize Cell { get; set; } = new() { PixelWidth = 10, PixelHeight = 20 };
    internal string? Name { get; set; }
    internal GoodImageAction Action { get; set; } = GoodImageAction.Display;
    internal GoodImageFormat Format { get; set; } = GoodImageFormat.Png;
    /// <summary>Send the source at its own size and let the terminal apply the resize policy.</summary>
    internal bool Passthrough { get; set; }
    internal GoodImageRender Render { get; set; } = new();
    internal bool RequestStatus { get; set; }
    /// <summary>Raw headers appended to the last operation; later headers override earlier ones.</summary>
    internal string[] Headers { get; set; } = [];
    /// <summary>Reserve rows and leave the cursor below the image at column 1, like the other protocols.</summary>
    internal bool Place { get; set; } = true;
    /// <summary>Page height in rows; 0 when unknown. Read from the console by default.</summary>
    internal int ViewportHeight { get; set; } = ImagePlacement.GetViewportHeight();
}

/// <summary>Builds Good Image Protocol output for ConvertTo-GoodImage and ConvertTo-Sixel.</summary>
internal static class GoodImage {
    internal static (string Text, ImageSize Cells, Size Pixels) Encode(Stream source, GoodImageOptions options) {
        byte[] original = ReadAll(source);
        using var image = ImageBackend.Load(new MemoryStream(original));
        return Encode(image, original, options);
    }

    /// <param name="original">The encoded source, sent unchanged when it is already a single-frame PNG
    /// that needs no local resizing; null to always re-encode.</param>
    internal static (string Text, ImageSize Cells, Size Pixels) Encode(ImageBackend image, byte[]? original, GoodImageOptions options) {
        GoodImageRender requested = options.Render;
        bool named = options.Name is not null;
        if (options.Name is not null) GoodImageProtocol.CheckName(options.Name);
        if (requested.Crop.HasValue && !named)
            throw new ArgumentException("A crop needs -Name: the oneshot operation cannot crop, so the image is uploaded and rendered by name.");
        if (options.Action == GoodImageAction.Upload && !named)
            throw new ArgumentException("Upload needs -Name, the name later renders refer to.");

        // The cells follow the part of the source that is shown.
        (int sourceWidth, int sourceHeight) = (image.Width, image.Height);
        if (requested.Crop is { } crop) {
            if (crop.X >= image.Width || crop.Y >= image.Height)
                throw new ArgumentOutOfRangeException(nameof(options), "The crop starts outside the image.");
            sourceWidth = crop.Width > 0 ? Math.Min(crop.Width, image.Width - crop.X) : image.Width - crop.X;
            sourceHeight = crop.Height > 0 ? Math.Min(crop.Height, image.Height - crop.Y) : image.Height - crop.Y;
        }

        bool display = options.Action == GoodImageAction.Display;
        bool place = display && options.Place && !requested.UpdateCursor;
        ImageLayout layout = ImageLayout.Calculate(sourceWidth, sourceHeight, options.Width, options.Height, options.Cell, sixel: false);
        // Contour clamps r to the page and fits into what is left, so a placed image fits the page up front.
        if (place && options.ViewportHeight > 0 && layout.Cells.Height > options.ViewportHeight)
            layout = ImageLayout.Calculate(sourceWidth, sourceHeight, options.Width, options.ViewportHeight, options.Cell, sixel: false);
        ImageSize cells = layout.Cells;

        // Fit is done here, exactly, padded to whole cells. Any other policy, a crop or passthrough
        // sends the source at its own size and lets the terminal resize it into the requested cells.
        bool fitLocally = !options.Passthrough && !requested.Crop.HasValue && requested.Resize == GoodImageResize.ResizeToFit;
        if (fitLocally) {
            image.Resize(layout.Pixels.Width, layout.Pixels.Height, 0);
            image.Pad(cells, options.Cell);
        }
        else if (requested.Resize != GoodImageResize.ResizeToFit) {
            cells = new ImageSize(options.Width > 0 ? options.Width : cells.Width, options.Height > 0 ? options.Height : cells.Height);
            if (place && options.ViewportHeight > 0)
                cells = new ImageSize(cells.Width, Math.Min(cells.Height, options.ViewportHeight));
        }
        var pixels = new Size(image.Width, image.Height);

        byte[] body = options.Format == GoodImageFormat.Png && !fitLocally && original is not null &&
            IsPng(original) && image.FrameCount == 1
            ? original
            : image.FrameBytes(0, options.Format switch { GoodImageFormat.Rgb => 24, GoodImageFormat.Rgba => 32, _ => 100 });
        bool raw = options.Format is GoodImageFormat.Rgb or GoodImageFormat.Rgba;
        int rawWidth = raw ? pixels.Width : 0, rawHeight = raw ? pixels.Height : 0;

        bool tall = place && options.ViewportHeight > 0 && cells.Height >= options.ViewportHeight;
        var render = new GoodImageRender {
            Columns = cells.Width,
            Rows = cells.Height,
            Alignment = requested.Alignment,
            Resize = requested.Resize,
            Layer = requested.Layer,
            Crop = requested.Crop,
            // An image as tall as the page scrolls it and leaves the cursor below.
            UpdateCursor = requested.UpdateCursor || tall,
            AutoScroll = requested.AutoScroll || tall,
        };

        string text;
        if (!named) {
            text = GoodImageProtocol.Oneshot(body, render, options.Format, rawWidth, rawHeight, options.RequestStatus, options.Headers);
        }
        else if (!display) {
            text = GoodImageProtocol.Upload(options.Name!, body, options.Format, rawWidth, rawHeight, options.RequestStatus, options.Headers);
        }
        else {
            text = GoodImageProtocol.Upload(options.Name!, body, options.Format, rawWidth, rawHeight, options.RequestStatus) +
                GoodImageProtocol.Render(options.Name!, render, options.RequestStatus, options.Headers);
        }
        return (place ? Place(text, cells.Height, tall) : text, cells, pixels);
    }

    /// <summary>o=r for an image uploaded earlier. Placement needs the row count, so it applies only with Height.</summary>
    internal static string Render(GoodImageOptions options) {
        if (options.Name is null) throw new ArgumentException("Rendering by name needs -Name.");
        GoodImageRender requested = options.Render;
        bool place = options.Place && !requested.UpdateCursor && options.Height > 0;
        int rows = place && options.ViewportHeight > 0 ? Math.Min(options.Height, options.ViewportHeight) : options.Height;
        bool tall = place && options.ViewportHeight > 0 && rows >= options.ViewportHeight;
        var render = new GoodImageRender {
            Columns = options.Width,
            Rows = rows,
            Alignment = requested.Alignment,
            Resize = requested.Resize,
            Layer = requested.Layer,
            Crop = requested.Crop,
            UpdateCursor = requested.UpdateCursor || tall,
            AutoScroll = requested.AutoScroll || tall,
        };
        string text = GoodImageProtocol.Render(options.Name, render, options.RequestStatus, options.Headers);
        return place ? Place(text, rows, tall) : text;
    }

    /// <summary>Like ImagePlacement.Wrap, but a page-tall image has already scrolled and moved the cursor (l,u).</summary>
    private static string Place(string sequence, int rows, bool tall) =>
        tall ? sequence + "\r" : ImagePlacement.Begin(rows) + sequence + ImagePlacement.End(rows);

    internal static bool IsPng(byte[] data) =>
        data.Length >= 8 && data[0] == 0x89 && data[1] == (byte)'P' && data[2] == (byte)'N' && data[3] == (byte)'G' &&
        data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A;

    private static byte[] ReadAll(Stream source) {
        if (source.CanSeek) source.Position = 0;
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }
}
