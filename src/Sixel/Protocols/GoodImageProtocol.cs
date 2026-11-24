using System.Globalization;
using System.Text;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Protocols;

// Preserve support for .NET Framework, which lacks these throw helpers.
#pragma warning disable CA1510, CA1513

/// <summary>How a Good Image Protocol image is scaled into its cell rectangle (header z).</summary>
public enum GoodImageResize {
    NoResize = 0,
    ResizeToFit = 1,
    ResizeToFill = 2,
    StretchToFill = 3,
}

/// <summary>Where the image sits inside its cell rectangle (header a, 1-9 in reading order).</summary>
public enum GoodImageAlignment {
    TopStart = 1,
    TopCenter = 2,
    TopEnd = 3,
    MiddleStart = 4,
    MiddleCenter = 5,
    MiddleEnd = 6,
    BottomStart = 7,
    BottomCenter = 8,
    BottomEnd = 9,
}

/// <summary>Whether the image is drawn below, instead of, or above the text in its cells (header L).</summary>
public enum GoodImageLayer {
    Below = 0,
    Replace = 1,
    Above = 2,
}

/// <summary>Uploaded pixel formats (header f).</summary>
public enum GoodImageFormat {
    Auto = 1,
    Rgb = 2,
    Rgba = 3,
    Png = 4,
}

/// <summary>Render headers shared by o=r and o=s. Zero cell counts leave the axis to the terminal.</summary>
public sealed class GoodImageRender {
    public int Columns { get; set; }
    public int Rows { get; set; }
    public GoodImageAlignment Alignment { get; set; } = GoodImageAlignment.TopStart;
    public GoodImageResize Resize { get; set; } = GoodImageResize.ResizeToFit;
    public GoodImageLayer Layer { get; set; } = GoodImageLayer.Replace;
    /// <summary>u: move the cursor below the image. Contour keeps the image's column; the spec says column 1.</summary>
    public bool UpdateCursor { get; set; }
    /// <summary>l: scroll the page when the image would extend below it.</summary>
    public bool AutoScroll { get; set; }
    /// <summary>Optional source crop in pixels (x,y,w,h). Only o=r accepts a crop.</summary>
    public (int X, int Y, int Width, int Height)? Crop { get; set; }
}

/// <summary>A <c>DCS ! g s=… ST</c> reply: a status code, or the o=q limits when Status is 8.</summary>
public sealed class GoodImageReply {
    public int Status { get; }
    public bool Success => Status == 0;
    public long? MaxImages { get; }
    public long? MaxBytes { get; }
    public long? MaxWidth { get; }
    public long? MaxHeight { get; }

    private GoodImageReply(int status, IReadOnlyDictionary<string, string> values) {
        Status = status;
        MaxImages = Number(values, "m");
        MaxBytes = Number(values, "b");
        MaxWidth = Number(values, "w");
        MaxHeight = Number(values, "h");
    }

    /// <summary>Parses one complete <c>ESC P ! g … ESC \</c> reply.</summary>
    public static bool TryParse(string response, out GoodImageReply? reply) {
        reply = null;
        if (response is null || !response.StartsWith(GoodImageProtocol.Start, StringComparison.Ordinal) ||
            !response.EndsWith(Constants.ST, StringComparison.Ordinal))
            return false;
        string body = response.Substring(GoodImageProtocol.Start.Length,
            response.Length - GoodImageProtocol.Start.Length - Constants.ST.Length);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string entry in body.Split(',')) {
            if (entry.Length == 0) continue;
            int equals = entry.IndexOf('=');
            if (equals < 0) values[entry] = string.Empty;
            else values[entry.Substring(0, equals)] = entry.Substring(equals + 1);
        }
        if (!values.TryGetValue("s", out string? status) ||
            !int.TryParse(status, NumberStyles.None, CultureInfo.InvariantCulture, out int code))
            return false;
        reply = new GoodImageReply(code, values);
        return true;
    }

    private static long? Number(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out string? value) &&
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) ? number : null;
}

/// <summary>
/// Preview encoder for the Good Image Protocol (DCS ! g). The specification is alpha and may change;
/// it is used only when asked for, never selected automatically.
/// </summary>
public static class GoodImageProtocol {
    internal static readonly string Start = Constants.ESC + "P!g";
    /// <summary>Requests the terminal's limits; the reply is <c>DCS ! g s=8,m=,b=,w=,h= ST</c>.</summary>
    public static readonly string QueryLimits = Start + "o=q" + Constants.ST;
    /// <summary>The bodies the specification requires a terminal to accept: 16 MB, before Base64.</summary>
    public const int MaxBodyBytes = 16 * 1024 * 1024;

    /// <summary>Frames headers and an optional Base64 body. Booleans are headers without a value.</summary>
    public static string Encode(IEnumerable<string> headers, byte[]? body = null) {
        if (headers is null) throw new ArgumentNullException(nameof(headers));
        var result = new StringBuilder(Start).Append(string.Join(",", headers));
        if (body is not null) {
            if (body.Length == 0) throw new ArgumentException("An image body cannot be empty.", nameof(body));
            if (body.Length > MaxBodyBytes)
                throw new ArgumentException("The image body exceeds the protocol's 16 MB minimum limit.", nameof(body));
            result.Append(";!").Append(Convert.ToBase64String(body));
        }
        return result.Append(Constants.ST).ToString();
    }

    /// <summary>o=s: uploads and renders a PNG at the cursor in one sequence.</summary>
    public static string Oneshot(byte[] png, GoodImageRender render) => Oneshot(png, render, GoodImageFormat.Png);

    /// <summary>
    /// o=s: uploads and renders at the cursor in one sequence. Raw RGB/RGBA need their pixel size.
    /// Extra headers are appended last; the protocol lets a later header override an earlier one.
    /// </summary>
    public static string Oneshot(byte[] data, GoodImageRender render, GoodImageFormat format,
        int width = 0, int height = 0, bool requestStatus = false, IEnumerable<string>? headers = null) {
        if (render is null) throw new ArgumentNullException(nameof(render));
        if (render.Crop.HasValue) throw new ArgumentException("Oneshot rendering has no crop; upload and render by name.", nameof(render));
        var entries = new List<string> { "o=s", Number('f', (int)format) };
        AddPixels(entries, data, format, width, height);
        AddRender(entries, render);
        if (requestStatus) entries.Add("s");
        if (headers is not null) entries.AddRange(headers);
        return Encode(entries, data);
    }

    /// <summary>o=u: stores a named image for later o=r renders.</summary>
    public static string Upload(string name, byte[] data, GoodImageFormat format = GoodImageFormat.Png,
        int width = 0, int height = 0, bool requestStatus = false, IEnumerable<string>? headers = null) {
        var entries = new List<string> { "o=u", "n=" + CheckName(name), Number('f', (int)format) };
        AddPixels(entries, data, format, width, height);
        if (requestStatus) entries.Add("s");
        if (headers is not null) entries.AddRange(headers);
        return Encode(entries, data);
    }

    private static void AddPixels(List<string> headers, byte[] data, GoodImageFormat format, int width, int height) {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (format is GoodImageFormat.Rgb or GoodImageFormat.Rgba) {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Raw RGB and RGBA images need a pixel width and height.", nameof(width));
            long expected = (long)width * height * (format == GoodImageFormat.Rgb ? 3 : 4);
            if (data.LongLength != expected)
                throw new ArgumentException("Raw pixel data does not match the width and height.", nameof(data));
        }
        if (width > 0) headers.Add(Number('w', width));
        if (height > 0) headers.Add(Number('h', height));
    }

    /// <summary>o=r: renders a named image at the cursor.</summary>
    public static string Render(string name, GoodImageRender render, bool requestStatus = false) =>
        Render(name, render, requestStatus, null);

    /// <summary>o=r with extra headers appended last.</summary>
    public static string Render(string name, GoodImageRender render, bool requestStatus, IEnumerable<string>? headers) {
        if (render is null) throw new ArgumentNullException(nameof(render));
        var entries = new List<string> { "o=r", "n=" + CheckName(name) };
        AddRender(entries, render);
        if (render.Crop is { } crop) {
            if (crop.X < 0 || crop.Y < 0 || crop.Width < 0 || crop.Height < 0)
                throw new ArgumentOutOfRangeException(nameof(render), "Crop values cannot be negative.");
            entries.Add(Number('x', crop.X));
            entries.Add(Number('y', crop.Y));
            entries.Add(Number('w', crop.Width));
            entries.Add(Number('h', crop.Height));
        }
        if (requestStatus) entries.Add("s");
        if (headers is not null) entries.AddRange(headers);
        return Encode(entries);
    }

    /// <summary>o=d: releases a named image. Cells already showing it keep it.</summary>
    public static string Release(string name) => Encode(["o=d", "n=" + CheckName(name)]);

    /// <summary>Names are 1-512 characters of [A-Za-z0-9_].</summary>
    public static string CheckName(string name) {
        if (string.IsNullOrEmpty(name) || name.Length > 512 ||
            !name.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_'))
            throw new ArgumentException("Image names are 1-512 characters of A-Z, a-z, 0-9 and _.", nameof(name));
        return name;
    }

    private static void AddRender(List<string> headers, GoodImageRender render) {
        if (render.Columns < 0 || render.Rows < 0)
            throw new ArgumentOutOfRangeException(nameof(render), "Cell counts cannot be negative.");
        if (render.Columns > 0) headers.Add(Number('c', render.Columns));
        if (render.Rows > 0) headers.Add(Number('r', render.Rows));
        headers.Add(Number('z', (int)render.Resize));
        headers.Add(Number('a', (int)render.Alignment));
        if (render.Layer != GoodImageLayer.Replace) headers.Add(Number('L', (int)render.Layer));
        if (render.UpdateCursor) headers.Add("u");
        if (render.AutoScroll) headers.Add("l");
    }

    private static string Number(char key, int value) => key + "=" + value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Named Good Image Protocol images for applications that own terminal IO, mirroring KittySession.
/// Never reads Console. The exchange callback writes a request and returns one complete <c>DCS ! g</c> reply.
/// Disposing releases every name this session uploaded.
/// </summary>
public sealed class GoodImageSession : IDisposable {
    private readonly Action<string> write;
    private readonly Func<string, TimeSpan, string> exchange;
    private readonly HashSet<string> ownedNames = new(StringComparer.Ordinal);
    private bool disposed;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(1);
    public string[] OwnedNames => ownedNames.ToArray();

    public GoodImageSession(Action<string> write, Func<string, TimeSpan, string> exchange) {
        this.write = write ?? throw new ArgumentNullException(nameof(write));
        this.exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
    }

    /// <summary>Uploads a named image and waits for its status. A success makes the session own the name.</summary>
    public GoodImageReply Upload(string name, byte[] data, GoodImageFormat format = GoodImageFormat.Png, int width = 0, int height = 0) {
        GoodImageReply reply = Exchange(GoodImageProtocol.Upload(name, data, format, width, height, requestStatus: true));
        if (reply.Success) ownedNames.Add(name);
        return reply;
    }

    /// <summary>Renders a named image at the cursor and waits for its status (1: no such name).</summary>
    public GoodImageReply Render(string name, GoodImageRender render) =>
        Exchange(GoodImageProtocol.Render(name, render, requestStatus: true));

    /// <summary>Queries the terminal's limits (reply status 8).</summary>
    public GoodImageReply QueryLimits() => Exchange(GoodImageProtocol.QueryLimits);

    /// <summary>Releases a name; the protocol sends no reply for o=d.</summary>
    public void Release(string name) {
        CheckDisposed();
        write(GoodImageProtocol.Release(name));
        ownedNames.Remove(name);
    }

    public void Dispose() {
        if (disposed) return;
        foreach (string name in ownedNames.ToArray())
            write(GoodImageProtocol.Release(name));
        ownedNames.Clear();
        disposed = true;
    }

    private GoodImageReply Exchange(string request) {
        CheckDisposed();
        if (Timeout <= TimeSpan.Zero) throw new InvalidOperationException("Timeout must be positive.");
        string packet = exchange(request, Timeout);
        if (!GoodImageReply.TryParse(packet, out GoodImageReply? reply) || reply is null)
            throw new IOException("No complete Good Image Protocol reply was returned; terminal state is unknown.");
        return reply;
    }

    private void CheckDisposed() {
        if (disposed) throw new ObjectDisposedException(nameof(GoodImageSession));
    }
}
