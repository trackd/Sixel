using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Sixel.Protocol.Tests;

public sealed class GoodImageProtocolTests {
    private const string Dcs = "\x1bP!g";
    private const string St = "\x1b\\";
    private static readonly byte[] Body = [1, 2, 3, 4, 5];
    private static readonly string Base64Body = Convert.ToBase64String(Body);
    [Fact]
    public void OneshotUsesPngFitTopStartAndCellRectangle() {
        string sequence = GoodImageProtocol.Oneshot(Body, new GoodImageRender { Columns = 12, Rows = 6 });
        Assert.Equal(Dcs + "o=s,f=4,c=12,r=6,z=1,a=1;!" + Base64Body + St, sequence);
    }

    [Fact]
    public void BooleanHeadersHaveNoValue() {
        var render = new GoodImageRender { Columns = 3, Rows = 2, UpdateCursor = true, AutoScroll = true, Layer = GoodImageLayer.Above };
        Assert.Equal(Dcs + "o=s,f=4,c=3,r=2,z=1,a=1,L=2,u,l;!" + Base64Body + St, GoodImageProtocol.Oneshot(Body, render));
    }

    [Fact]
    public void OneshotRefusesACrop() {
        var render = new GoodImageRender { Crop = (0, 0, 4, 4) };
        Assert.Throws<ArgumentException>(() => GoodImageProtocol.Oneshot(Body, render));
    }

    [Fact]
    public void NamedUploadRenderReleaseAndQuery() {
        Assert.Equal(Dcs + "o=u,n=cog_1,f=4,s;!" + Base64Body + St, GoodImageProtocol.Upload("cog_1", Body, requestStatus: true));
        byte[] rgb = new byte[2 * 1 * 3];
        Assert.Equal(Dcs + "o=u,n=raw,f=2,w=2,h=1;!" + Convert.ToBase64String(rgb) + St,
            GoodImageProtocol.Upload("raw", rgb, GoodImageFormat.Rgb, 2, 1));
        var render = new GoodImageRender {
            Columns = 8, Rows = 4, Alignment = GoodImageAlignment.MiddleCenter, Resize = GoodImageResize.ResizeToFill,
            Layer = GoodImageLayer.Below, Crop = (10, 20, 30, 40),
        };
        Assert.Equal(Dcs + "o=r,n=cog_1,c=8,r=4,z=2,a=5,L=0,x=10,y=20,w=30,h=40,s" + St,
            GoodImageProtocol.Render("cog_1", render, requestStatus: true));
        Assert.Equal(Dcs + "o=d,n=cog_1" + St, GoodImageProtocol.Release("cog_1"));
        Assert.Equal(Dcs + "o=q" + St, GoodImageProtocol.QueryLimits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("dash-name")]
    [InlineData("ümlaut")]
    public void NamesAreValidated(string name) =>
        Assert.Throws<ArgumentException>(() => GoodImageProtocol.Release(name));

    [Fact]
    public void RawUploadsMustMatchTheirGeometry() =>
        Assert.Throws<ArgumentException>(() => GoodImageProtocol.Upload("raw", new byte[5], GoodImageFormat.Rgba, 2, 1));

    [Fact]
    public void EmptyBodiesAreRefused() =>
        Assert.Throws<ArgumentException>(() => GoodImageProtocol.Oneshot([], new GoodImageRender()));

    [Theory]
    [InlineData("\x1bP!gs=0\x1b\\", 0, true)]
    [InlineData("\x1bP!gs=1\x1b\\", 1, false)]
    [InlineData("\x1bP!gs=3\x1b\\", 3, false)]
    public void StatusRepliesParse(string response, int status, bool success) {
        Assert.True(GoodImageReply.TryParse(response, out GoodImageReply? reply));
        Assert.Equal(status, reply!.Status);
        Assert.Equal(success, reply.Success);
    }

    [Fact]
    public void LimitsReplyParses() {
        Assert.True(GoodImageReply.TryParse("\x1bP!gs=8,m=100,b=67108864,w=4096,h=4096\x1b\\", out GoodImageReply? reply));
        Assert.Equal(8, reply!.Status);
        Assert.Equal(100, reply.MaxImages);
        Assert.Equal(67108864, reply.MaxBytes);
        Assert.Equal(4096, reply.MaxWidth);
        Assert.Equal(4096, reply.MaxHeight);
    }

    [Theory]
    [InlineData("\x1b_Gi=31;OK\x1b\\")]
    [InlineData("\x1bP!gm=1\x1b\\")]
    [InlineData("\x1bP!gs=x\x1b\\")]
    [InlineData("\x1bP!gs=0")]
    public void OtherRepliesAreNotStatus(string response) =>
        Assert.False(GoodImageReply.TryParse(response, out _));

    [Fact]
    public void ConvertToFramesAPaddedPngInTheSharedPlacement() {
        (ImageSize size, string data) = ConvertTo.ConsoleImage(ImageProtocol.GoodImageProtocol, Png(31, 21), 256, 0, 0, true);
        Assert.Equal(4, size.Width);
        Assert.Equal(2, size.Height);
        string prefix = ImagePlacement.Begin(2) + Dcs + "o=s,f=4,c=4,r=2,z=1,a=1;!";
        Assert.StartsWith(prefix, data, StringComparison.Ordinal);
        Assert.EndsWith(St + ImagePlacement.End(2), data, StringComparison.Ordinal);
        string base64 = data.Substring(prefix.Length, data.Length - prefix.Length - St.Length - ImagePlacement.End(2).Length);
        byte[] png = Convert.FromBase64String(base64);
        Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, png.Take(4).ToArray());
        using var decoded = Image.Load<Rgba32>(png);
        Assert.Equal(40, decoded.Width);
        Assert.Equal(40, decoded.Height);
        Assert.Equal(255, decoded[30, 20].A);
        Assert.Equal(0, decoded[39, 39].A);
    }

    [Fact]
    public void SessionOwnsUploadedNamesAndReleasesThemOnDispose() {
        var written = new List<string>();
        var requests = new List<string>();
        var replies = new Queue<string>(["\x1bP!gs=0\x1b\\", "\x1bP!gs=2\x1b\\", "\x1bP!gs=1\x1b\\", "\x1bP!gs=8,m=100,b=1,w=2,h=3\x1b\\"]);
        using (var session = new GoodImageSession(written.Add, (request, _) => { requests.Add(request); return replies.Dequeue(); })) {
            Assert.True(session.Upload("a", Body).Success);
            Assert.False(session.Upload("b", Body).Success);
            Assert.Equal(["a"], session.OwnedNames);
            Assert.Equal(1, session.Render("missing", new GoodImageRender()).Status);
            Assert.Equal(100, session.QueryLimits().MaxImages);
            Assert.Equal(GoodImageProtocol.Upload("a", Body, requestStatus: true), requests[0]);
            Assert.Equal(Dcs + "o=r,n=missing,z=1,a=1,s" + St, requests[2]);
            Assert.Equal(GoodImageProtocol.QueryLimits, requests[3]);
        }
        Assert.Equal([Dcs + "o=d,n=a" + St], written);
    }

    [Fact]
    public void SessionRejectsIncompleteReplies() {
        using var session = new GoodImageSession(_ => { }, (_, _) => "\x1b_Gi=1;OK\x1b\\");
        Assert.Throws<IOException>(() => session.Upload("a", Body));
        Assert.Empty(session.OwnedNames);
    }

    private static MemoryStream Png(int width, int height) {
        using var image = new Image<Rgba32>(width, height, new Rgba32(255, 40, 20));
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }
}
