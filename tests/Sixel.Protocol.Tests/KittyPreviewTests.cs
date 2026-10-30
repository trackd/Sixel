using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Sixel.Protocols;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Sixel.Protocol.Tests;

public sealed partial class KittyPreviewTests {
    [GeneratedRegex("\u001b_G(?<header>[^;\u001b]+)(?:;(?<data>[^\u001b]*))?\u001b\\\\")]
    private static partial Regex PacketPattern();
    private static MatchCollection Packets(string text) => PacketPattern().Matches(text);

    [Theory]
    [InlineData(1)]
    [InlineData(3071)]
    [InlineData(3072)]
    [InlineData(3073)]
    [InlineData(6144)]
    [InlineData(10000)]
    public void DirectChunksRoundTripAtBoundaries(int size) {
        byte[] source = Enumerable.Range(0, size).Select(index => (byte)index).ToArray();
        MatchCollection packets = Packets(KittyProtocol.Encode(["a=T", "f=100", "i=4294967295"], source));
        Assert.Equal((Convert.ToBase64String(source).Length + 4095) / 4096, packets.Count);
        for (int index = 0; index < packets.Count; index++) {
            string header = packets[index].Groups["header"].Value;
            Assert.EndsWith(index + 1 < packets.Count ? "m=1" : "m=0", header);
            Assert.Contains("q=2", header);
            Assert.InRange(packets[index].Groups["data"].Length, 1, 4096);
            if (index > 0) Assert.StartsWith("q=2,m=", header);
        }
        Assert.Equal(source, Decode(packets));
    }

    [Fact]
    public void FrameContinuationsRetainActionButNotIdentifiers() {
        MatchCollection packets = Packets(KittyProtocol.Encode(["a=f", "i=3", "f=100", "z=-1"], new byte[7000]));
        Assert.Equal("a=f,q=2,m=1", packets[1].Groups["header"].Value);
        Assert.Equal("a=f,q=2,m=0", packets[2].Groups["header"].Value);
    }

    [Theory]
    [InlineData("f")]
    [InlineData("t")]
    [InlineData("s")]
    public void ReferencesAreBase64Utf8AndNotCompressedOrChunked(string medium) {
        byte[] name = Encoding.UTF8.GetBytes("/tmp/图像");
        Match packet = Assert.Single(Packets(KittyProtocol.Encode(["a=t", "f=100", "t=" + medium, "o=z", "S=512", "O=12"], name)).Cast<Match>());
        Assert.Equal(name, Convert.FromBase64String(packet.Groups["data"].Value));
        Assert.DoesNotContain("m=", packet.Groups["header"].Value);
        Assert.Contains("S=512", packet.Groups["header"].Value);
    }

    [Theory]
    [InlineData("a=p,i=1,p=2,x=3,y=4,w=5,h=6,X=1,Y=2,c=4,r=3,C=1,z=-2")]
    [InlineData("a=p,I=1,P=5,Q=3,H=-4,V=7")]
    [InlineData("a=p,i=1,U=1,c=5,r=6")]
    [InlineData("a=a,i=1,s=3,v=1,r=2,z=-1,c=2")]
    [InlineData("a=c,i=1,c=2,r=3,x=5,y=6,X=7,Y=8,w=10,h=11,C=1")]
    public void ControlCommandsHaveNoPayload(string controls) {
        string result = KittyProtocol.Encode(controls.Split(','));
        Assert.Equal("\u001b_G" + controls + ",q=2\u001b\\", result);
    }

    [Theory]
    [InlineData("aAcCnNiIpPqQrRxXyYzZfF")]
    public void EveryDeleteSelectorPreservesCase(string selectors) {
        foreach (char selector in selectors)
            Assert.Contains("d=" + selector, KittyProtocol.Encode(["a=d", "d=" + selector, "i=1"]));
    }

    [Theory]
    [InlineData("a=t,f=100,m=1")]
    [InlineData("a=t,f=100,i=1,I=2")]
    [InlineData("a=t,f=100,q=2,q=0")]
    [InlineData("a=t,f=100,N=4294967296")]
    [InlineData("a=t,f=100,i=-1")]
    [InlineData("a=t,f=100,z=2147483648")]
    [InlineData("a=t,f=100,o=g")]
    [InlineData("a=t,f=100,q=2\u001b\\")]
    [InlineData("a=t,f=32")]
    public void InvalidControlsAreRejected(string controls) =>
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(controls.Split(','), [1]));

    [Fact]
    public void RejectsMissingIdentifiersAndRelativeVirtualPlacements() {
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=p"]));
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=p", "i=1", "U=1", "P=2"]));
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=d"], [1]));
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=t", "f=100"]));
    }

    [Fact]
    public void QueryAndTransientHintAreAvailableWithoutTerminalIo() {
        string query = KittyProtocol.Encode(["a=q", "i=31", "f=24", "s=1", "v=1", "q=0"], [0, 0, 0]);
        Assert.Equal("\u001b_Ga=q,i=31,f=24,s=1,v=1,q=0,m=0;AAAA\u001b\\", query);
        Assert.Contains("N=1", KittyProtocol.Encode(["a=T", "N=1", "f=100"], [1]));
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=T", "f=24", "s=2", "v=1"], [1, 2, 3]));
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=a", "i=1", "s=4"]));
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=f", "i=1", "f=100", "X=2"], [1]));
        Assert.Throws<ArgumentException>(() => KittyProtocol.Encode(["a=p", "i=1", "o=z"]));
    }

    [Theory]
    [InlineData(3, 0, 30, 15, 3, 1)]
    [InlineData(0, 2, 80, 40, 8, 2)]
    [InlineData(3, 2, 30, 15, 3, 1)]
    public void SizingPreservesAspectRatioWithinCellConstraints(int width, int height, int pixelsWide, int pixelsHigh, int columns, int rows) {
        using var image = new Image<Rgba32>(40, 20);
        using var input = new MemoryStream();
        image.SaveAsPng(input);
        input.Position = 0;
        var result = KittyImage.Encode(input, new KittyImageOptions { Width = width, Height = height });
        Assert.Equal(new Size(pixelsWide, pixelsHigh), result.Pixels);
        Assert.Equal(columns, result.Cells.Width);
        Assert.Equal(rows, result.Cells.Height);
        using var decoded = Image.Load<Rgba32>(Decode(Packets(result.Text)));
        Assert.Equal(result.Pixels, decoded.Size);
    }

    [Theory]
    [InlineData(20, false, 'T', true)]
    [InlineData(60, false, 'T', true)]
    [InlineData(60, true, 'T', false)]
    [InlineData(60, false, 't', false)]
    public void ExplicitCursorMovementReservesRowsWithoutImplicitGraphicsMovement(int height, bool noMove, char action, bool moves) {
        using var image = new Image<Rgba32>(20, height);
        using var source = new MemoryStream();
        image.SaveAsPng(source);
        source.Position = 0;
        var result = KittyImage.Encode(source, new KittyImageOptions {
            ExplicitCursorMove = true, NoCursorMove = noMove, Action = action
        });
        string header = Packets(result.Text)[0].Groups["header"].Value;
        Assert.Equal(moves || noMove, header.Split(',').Contains("C=1"));
        int end = result.Text.LastIndexOf("\u001b\\", StringComparison.Ordinal) + 2;
        Assert.Equal(moves ? string.Concat(Enumerable.Repeat("\r\n", height / 20 - 1)) + "\r" : "", result.Text.Substring(end));
    }

    [Fact]
    public void ZlibHasRfc1950EnvelopeAndPngUncompressedSize() {
        byte[] original = Enumerable.Range(0, 9000).Select(value => (byte)value).ToArray();
        MatchCollection packets = Packets(KittyProtocol.Encode(["f=100", "o=z"], original));
        Assert.Contains("S=9000", packets[0].Groups["header"].Value);
        using var compressed = new MemoryStream(Decode(packets));
        using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);
        using var expanded = new MemoryStream();
        inflater.CopyTo(expanded);
        Assert.Equal(original, expanded.ToArray());
    }

    [Fact]
    public void TmuxWrapsEachPacketAndDoublesEveryEscape() {
        string raw = KittyProtocol.Encode(["a=p", "i=1"]);
        Assert.Equal("\u001bPtmux;" + raw.Replace("\u001b", "\u001b\u001b") + "\u001b\\",
            KittyProtocol.Encode(["a=p", "i=1"], tmux: true));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(100)]
    public void ImageFormatsPreservePixelsAndPartialCellGeometry(int format) {
        using var image = new Image<Rgba32>(11, 21, new Rgba32(12, 34, 56, 78));
        using var input = new MemoryStream();
        image.SaveAsPng(input);
        input.Position = 0;
        var result = KittyImage.Encode(input, new KittyImageOptions { Format = format });
        Assert.Equal(11, result.Pixels.Width);
        Assert.Equal(21, result.Pixels.Height);
        Assert.Equal(2, result.Cells.Width);
        Assert.Equal(2, result.Cells.Height);
        MatchCollection packets = Packets(result.Text);
        Assert.DoesNotContain(",c=", packets[0].Groups["header"].Value);
        Assert.DoesNotContain(",r=", packets[0].Groups["header"].Value);
        byte[] pixels = Decode(packets);
        if (format == 100) {
            using var decoded = Image.Load<Rgba32>(pixels);
            Assert.Equal(image[0, 0], decoded[0, 0]);
            Assert.Equal(image.Size, decoded.Size);
        }
        else {
            Assert.Equal(11 * 21 * (format / 8), pixels.Length);
            Assert.Equal(new byte[] { 12, 34, 56 }, pixels.Take(3));
            if (format == 32) Assert.Equal(78, pixels[3]);
        }
        Assert.True(input.CanRead);
    }

    [Fact]
    public void AnimationUsesFullFramesExactDelaysAndLoopControls() {
        using var image = new Image<Rgba32>(2, 2, new Rgba32(255, 0, 0));
        image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 7;
        using var second = new Image<Rgba32>(2, 2, new Rgba32(0, 255, 0));
        second.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 0;
        image.Frames.AddFrame(second.Frames.RootFrame);
        image.Metadata.GetGifMetadata().RepeatCount = 2;
        using var input = new MemoryStream();
        image.SaveAsGif(input);
        input.Position = 0;
        var result = KittyImage.Encode(input, new KittyImageOptions { ImageId = 9, Animate = true });
        Match[] packets = Packets(result.Text).Cast<Match>().ToArray();
        Assert.Equal(4, packets.Length);
        Assert.Contains("r=1,z=70", packets[1].Groups["header"].Value);
        Assert.Contains("a=f,i=9,q=2,t=d,f=100,X=1,z=-1", packets[2].Groups["header"].Value);
        Assert.Contains("s=3,v=3", packets[3].Groups["header"].Value);
        using var decoded = Image.Load<Rgba32>(Convert.FromBase64String(packets[2].Groups["data"].Value));
        Assert.Equal(new Rgba32(0, 255, 0), decoded[0, 0]);
    }

    [Fact]
    public void PlaceholdersEncodeAllIdBitsAndExplicitCoordinates() {
        string result = KittyPlaceholder.Encode(0x0200002a, 0x010203, 2, 2);
        Assert.StartsWith("\u001b[38;2;0;0;42;58;2;1;2;3m", result);
        Assert.Contains("\U0010eeee\u0305\u030d\u030e", result);
        Assert.Contains("\U0010eeee\u030d\u0305\u030e", result);
        Assert.Equal(2, result.Split("\r\n").Length - 1);
        Assert.EndsWith("\u001b[39;59m", result);
        Assert.Throws<ArgumentOutOfRangeException>(() => KittyPlaceholder.Encode(1, 0x1000000, 1, 1));
    }

    private static byte[] Decode(MatchCollection packets) => Convert.FromBase64String(string.Concat(packets.Cast<Match>().Select(packet => packet.Groups["data"].Value)));
}
