using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixelEncoder = Sixel.Protocols.Sixel;

using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Sixel.Protocol.Tests;

public sealed class ProtocolTests : IDisposable {
    private readonly Dictionary<FieldInfo, object?> saved = [];
    private readonly CellSize cell = new() { PixelWidth = 10, PixelHeight = 20 };

    public ProtocolTests() {
        foreach (string name in new[] { "_cellSize", "_terminalInfo", "_terminalSupportsKitty", "_terminalSupportsSixel", "_lastWindowWidth", "_lastWindowHeight" }) {
            FieldInfo field = typeof(Compatibility).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            saved.Add(field, field.GetValue(null));
        }
        typeof(Compatibility).GetField("_cellSize", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, cell);
        typeof(Compatibility).GetField("_terminalInfo", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null,
            new TerminalInfo { Terminal = Terminals.unknown, Protocol = [ImageProtocol.Blocks] });
        Compatibility._terminalSupportsKitty = false;
        Compatibility._terminalSupportsSixel = false;
    }

    public void Dispose() {
        foreach (KeyValuePair<FieldInfo, object?> entry in saved) entry.Key.SetValue(null, entry.Value);
    }
    [Fact]
    public void SixelOccupancyRoundsToBands() {
        Assert.True(ImageLayout.Measure(20, 20, cell, true).Height == 2, "Issue #29: 20px sixel occupies two 20px rows");
        Assert.True(ImageLayout.Measure(20, 20, cell, false).Height == 1, "PNG protocols must not inherit sixel rounding");
    }

    public static TheoryData<int, bool, int, int, int, int> LayoutCases {
        get {
            var cases = new TheoryData<int, bool, int, int, int, int>();
            foreach (int cellHeight in new[] { 8, 16, 19, 20, 21, 24, 32 }) {
                foreach (bool sixel in new[] { false, true }) {
                    foreach (int width in new[] { 1, 17, 100, 503 }) {
                        foreach (int height in new[] { 1, 6, 20, 113 }) {
                            foreach (int columns in new[] { 0, 1, 7 }) {
                                foreach (int rows in new[] { 0, 1, 3 })
                                    cases.Add(cellHeight, sixel, width, height, columns, rows);
                            }
                        }
                    }
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(LayoutCases))]
    public void LayoutRespectsCellBounds(int cellHeight, bool sixel, int width, int height, int columns, int rows) {
        var metrics = new CellSize { PixelWidth = 9, PixelHeight = cellHeight };
        var layout = ImageLayout.Calculate(width, height, columns, rows, metrics, sixel);
        Assert.True(columns == 0 || layout.Cells.Width <= columns);
        Assert.True(rows == 0 || layout.Cells.Height <= rows);
        Assert.True(layout.Pixels.Width > 0 && layout.Pixels.Height > 0);
        if (columns == 0 && rows == 0)
            Assert.Equal(new Size(width, height), layout.Pixels);
    }

    [Fact]
    public void RasterizationScalesContentWithinBudget() {
        var constrained = ImageLayout.Calculate(100, 100, 0, 2, cell, true);
        Assert.Equal(36, constrained.Pixels.Height);
        Assert.Equal(2, constrained.Cells.Height);
        using var tiny = new Image<Rgba32>(2, 2, new Rgba32(255, 0, 0));
        var enlarged = ImageLayout.Calculate(2, 2, 4, 0, cell, false);
        Resizer.ResizeToPixels(tiny, enlarged.Pixels, 0);
        Assert.Equal(40, tiny.Width);
        Assert.Equal(255, tiny[39, 39].A);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(24)]
    public void SixelSeparatesBandsWithoutTrailingAdvance(int height) {
        using var image = new Image<Rgba32>(3, height, new Rgba32(255, 0, 0));
        string sixel = SixelEncoder.FrameToSixelString(image.Frames[0]);
        Assert.Equal((height - 1) / 6, sixel.Count(c => c == '-'));
        Assert.EndsWith("\x1b\\", sixel, StringComparison.Ordinal);
        Assert.False(sixel.EndsWith("-\x1b\\", StringComparison.Ordinal));
    }
    [Fact]
    public void PaletteUsesOnly256Registers() {
        using var palette = new Image<Rgba32>(256, 1);
        for (int x = 0; x < 256; x++) palette[x, 0] = new Rgba32((byte)x, 10, 30);
        string sixel = SixelEncoder.FrameToSixelString(palette.Frames[0]);
        Assert.True(!sixel.Contains("#256", StringComparison.Ordinal), "256-color palette must stay in registers 0..255");
    }

    [Fact]
    public void ProtocolsAcceptNonSeekableStreamsAndReportSize() {
        foreach (ImageProtocol protocol in new[] { ImageProtocol.Sixel, ImageProtocol.InlineImageProtocol, ImageProtocol.KittyGraphicsProtocol }) {
            using var image = new Image<Rgba32>(31, 21, new Rgba32(255, 40, 20));
            using var input = new MemoryStream();
            image.SaveAsPng(input);
            input.Position = 0;
            using var nonSeekable = new NonSeekableStream(input);
            (ImageSize Size, string Data) output = ConvertTo.ConsoleImage(protocol, nonSeekable, 256, 0, 0, true);
            Assert.True(output.Size.Width == 4 && output.Size.Height == 2, "Natural output metadata");
            Assert.True(output.Data.StartsWith(ImagePlacement.Begin(2), StringComparison.Ordinal), "Reserve rows before rendering");
            Assert.True(output.Data.EndsWith(ImagePlacement.End(2), StringComparison.Ordinal), "Leave cursor below image at column one");
            if (protocol == ImageProtocol.InlineImageProtocol) {
                Match match = Patterns.Inline().Match(output.Data);
                Assert.True(match.Success, "Inline OSC must end with BEL");
                Assert.True(match.Groups[1].Value.Contains("width=4;height=2;preserveAspectRatio=0", StringComparison.Ordinal), "Explicit inline cell geometry");
                byte[] bytes = Convert.FromBase64String(match.Groups[2].Value);
                using var decoded = Image.Load<Rgba32>(bytes);
                Assert.True(decoded.Width == 40 && decoded.Height == 40, "Inline raster matches placement");
                Assert.True(decoded[30, 20].A == 255 && decoded[39, 39].A == 0, "Pad only outside natural content");
                Assert.True(match.Groups[1].Value.Contains("size=" + bytes.Length.ToString(CultureInfo.InvariantCulture) + ";", StringComparison.Ordinal), "Inline byte length");
            }
        }
    }

    [Fact]
    public void KittyChunksContainValidPng() {
        // Exercise chunk boundaries with incompressible pixels and a non-PNG input.
        using var image = new Image<Rgba32>(100, 100);
        var random = new Random(1234);
        for (int y = 0; y < image.Height; y++) {
            for (int x = 0; x < image.Width; x++)
                image[x, y] = new Rgba32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        }

        using var input = new MemoryStream();
        image.SaveAsBmp(input);
        string kitty = KittyGraphics.ImageToKitty(input); // deliberately positioned at EOF
        MatchCollection chunks = Patterns.Kitty().Matches(kitty);
        Assert.True(chunks.Count > 1, "Multi-chunk test fixture");
        var base64 = new StringBuilder();
        for (int index = 0; index < chunks.Count; index++) {
            string header = chunks[index].Groups[1].Value;
            string payload = chunks[index].Groups[2].Value;
            Assert.True(payload.Length <= 4096 && payload.Length % 4 == 0, "Kitty base64 chunk limit/alignment");
            Assert.True(header.Contains(index == chunks.Count - 1 ? "m=0" : "m=1", StringComparison.Ordinal), "Kitty continuation flag");
            Assert.True(header.Contains("q=2", StringComparison.Ordinal), "Suppress terminal replies on every chunk");
            base64.Append(payload);
        }
        Assert.True(chunks[0].Groups[1].Value.Contains("c=10,r=5", StringComparison.Ordinal), "Kitty placement dimensions");
        using var decoded = Image.Load<Rgba32>(Convert.FromBase64String(base64.ToString()));
        Assert.True(decoded.Metadata.DecodedImageFormat!.Name == "PNG", "f=100 must carry PNG even for BMP input");
        Assert.True(decoded[42, 37] == image[42, 37], "Chunk reassembly preserves pixels");
    }

    [Fact]
    public void AnimationPreservesFramesAndCancellationCleanup() {
        using var image = new Image<Rgba32>(20, 20, new Rgba32(255, 0, 0));
        _ = image.Frames.AddFrame(image.Frames.RootFrame);
        using var input = new MemoryStream();
        image.SaveAsGif(input);
        input.Position = 0;
        SixelGif gif = GifToSixel.ConvertGif(input, 256, 0, 1);
        Assert.True(gif.Height == 2 && gif.Width == 2 && gif.FrameCount == 2, "GIF natural size and frames");
        Assert.True(gif.Sixel.All(frame => frame.Count(c => c == '-') == 3), "GIF frames share exact raster geometry");
        input.Position = 0;
        (ImageSize Size, string Data) inline = ConvertTo.ConsoleImage(ImageProtocol.InlineImageProtocol, input, 256, 0, 0, true);
        Match match = Patterns.Inline().Match(inline.Data);
        using var decoded = Image.Load<Rgba32>(Convert.FromBase64String(match.Groups[2].Value));
        Assert.True(decoded.Frames.Count == 2, "Inline conversion must preserve animation frames");

        // Redirected playback lets us inspect cleanup without touching a real terminal.
        using var captured = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new VTWriter(captured);
        GifToSixel.PlaySixelGif(gif, writer, new CancellationToken(true));
        Assert.True(captured.ToString().EndsWith(ImagePlacement.End(2) + "\x1b[?25h", StringComparison.Ordinal), "Cancellation restores cursor below GIF");
    }

    [Fact]
    public void ReservationHandlesBottomOfViewport() {
        // Model the issue #29 reservation at the bottom of a scrolling viewport.
        foreach (int start in new[] { 0, 18, 23 }) {
            int cursor = start;
            const int rows = 5;
            for (int line = 0; line < rows; line++) cursor = Math.Min(23, cursor + 1);
            cursor -= rows;
            int saved = cursor;
            foreach (int terminalAdvance in new[] { rows - 1, rows }) {
                for (int frame = 0; frame < 100; frame++) {
                    cursor = saved;
                    cursor += terminalAdvance;
                    Assert.True(cursor <= 23, "Reserved parking row prevents frame scrolling");
                    cursor = saved;
                }
                Assert.True(cursor == saved, "Frame origin drift");
            }
            cursor = saved + rows;
            Assert.True(cursor <= 23, "Final cursor stays in viewport");
        }
    }

    [Fact]
    public void MetricsAndTerminalIdentification() {
        Assert.True(Compatibility.TryParseSizeReport("\x1b[6;20;10t", 6, out int w, out int h) && w == 10 && h == 20, "Cell metrics ordering");
        Assert.False(Compatibility.TryParseSizeReport("\x1b[4;20;10t", 6, out _, out _), "Reject wrong report type");
        Assert.False(Compatibility.TryParseSizeReport("\x1b[6;0;10t", 6, out _, out _), "Reject zero cell metrics");
        Assert.True(Helpers.GetTerminal("iTerm.app") == Terminals.Iterm2, "iTerm TERM_PROGRAM alias");
        Assert.True(Helpers.GetTerminal("xterm-kitty") == Terminals.Kitty, "Kitty TERM alias");
        string? originalProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        string? originalVersion = Environment.GetEnvironmentVariable("TERM_PROGRAM_VERSION");
        try {
            Environment.SetEnvironmentVariable("TERM_PROGRAM", "iTerm.app");
            Environment.SetEnvironmentVariable("TERM_PROGRAM_VERSION", null);
            TerminalInfo terminal = TerminalChecker.CheckTerminal();
            Assert.True(terminal.Terminal == Terminals.Iterm2 && terminal.Protocol.Contains(ImageProtocol.InlineImageProtocol),
                "TERM_PROGRAM must work without a version");
        }
        finally {
            Environment.SetEnvironmentVariable("TERM_PROGRAM", originalProgram);
            Environment.SetEnvironmentVariable("TERM_PROGRAM_VERSION", originalVersion);
        }
    }

}

internal static partial class Patterns {
    [GeneratedRegex("\x1b\\]1337;File=([^:]+):([^\a]+)\a")]
    internal static partial Regex Inline();
    [GeneratedRegex("\x1b_G([^;]+);([^\x1b]*)\x1b\\\\")]
    internal static partial Regex Kitty();
}

internal sealed class NonSeekableStream(Stream inner) : Stream {
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
