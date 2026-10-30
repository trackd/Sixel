using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Sixel.Protocols;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Sixel.Protocol.Tests;

public sealed class KittyClientTests {
    [Theory]
    [InlineData(4)]
    [InlineData(84)]
    [InlineData(680)]
    [InlineData(4096)]
    public void SmallerPacketsAndWriterRoundTrip(int chunkSize) {
        byte[] payload = Enumerable.Range(0, 4000).Select(value => (byte)value).ToArray();
        using var writer = new StringWriter();
        KittyProtocol.WriteTo(writer, ["a=f", "i=42", "f=100"], payload, chunkSize: chunkSize);
        string[] packets = writer.ToString().Split("\u001b\\", StringSplitOptions.RemoveEmptyEntries);
        var encoded = new System.Text.StringBuilder();
        foreach (string packet in packets) {
            Assert.Contains("a=f", packet);
            string data = packet.Substring(packet.IndexOf(';') + 1);
            Assert.InRange(data.Length, 1, chunkSize);
            encoded.Append(data);
        }
        Assert.Equal(payload, Convert.FromBase64String(encoded.ToString()));
        writer.Write("still open");
    }

    [Fact]
    public void InvalidChunkSizeWritesNothing() {
        using var writer = new StringWriter();
        Assert.Throws<ArgumentOutOfRangeException>(() => KittyProtocol.WriteTo(writer, ["f=100"], [1], chunkSize: 7));
        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public void CompressedReferenceCarriesOriginalPngLength() {
        byte[] payload = Enumerable.Repeat((byte)17, 1000).ToArray();
        using var transfer = KittyTransfer.CreateTemporaryFile(payload, compress: true);
        Assert.Contains("S=1000,o=z", transfer.Encode(["f=100"]));
        using var compressed = File.OpenRead(transfer.Reference);
        using var inflater = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionMode.Decompress);
        using var decoded = new MemoryStream();
        inflater.CopyTo(decoded);
        Assert.Equal(payload, decoded.ToArray());
    }

    [Fact]
    public void ApngPosterIsNotIncludedInAnimation() {
        using var image = new Image<Rgba32>(1, 1);
        image.Frames.AddFrame(image.Frames.RootFrame);
        image.Frames.AddFrame(image.Frames.RootFrame);
        image.Metadata.GetPngMetadata().AnimateRootFrame = false;
        for (int index = 0; index < 3; index++)
            image.Frames[index].Metadata.GetPngMetadata().FrameDelay = new Rational((uint)(index + 1), 10);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        string encoded = KittyImage.Encode(stream, new KittyImageOptions { Animate = true, ImageId = 42 }).Text;
        Assert.Contains("r=1,z=200", encoded);
        Assert.Contains("X=1,z=300", encoded);
        Assert.DoesNotContain("z=100", encoded);
    }

    [Theory]
    [InlineData("i=4294967295,I=23,p=9;OK", true, null)]
    [InlineData("i=3;ENOENT:No such image", false, "ENOENT")]
    public void ParsesAcknowledgements(string body, bool success, string? error) {
        Assert.True(KittyResponse.TryParse("\u001b_G" + body + "\u001b\\", out var reply));
        Assert.Equal(success, reply!.Success);
        Assert.Equal(error, reply.ErrorCode);
    }

    [Theory]
    [InlineData("\u001b_Gi=1;OK")]
    [InlineData("\u001b_Gi=1,i=2;OK\u001b\\")]
    [InlineData("\u001b_Gi=4294967296;OK\u001b\\")]
    [InlineData("\u001b_Gi=1;OK\n\u001b\\")]
    [InlineData("\u001b_Gi=1;\u001b\\")]
    [InlineData("\u001b_Gq=2;OK\u001b\\")]
    public void RejectsMalformedReplies(string packet) => Assert.False(KittyResponse.TryParse(packet, out _));

    [Fact]
    public void SessionTracksAssignedIdsAndDeletesOnlyOwnedImages() {
        var written = new List<string>();
        using (var session = new KittySession(written.Add, (request, timeout) => {
            Assert.Contains("q=0", request);
            Assert.True(timeout > TimeSpan.Zero);
            return "\u001b_Gi=77,I=5;OK\u001b\\";
        })) {
            Assert.True(session.Execute(["a=t", "I=5", "f=100"], [1]).Success);
            Assert.Equal(new uint[] { 77 }, session.OwnedImageIds);
            session.Send(["a=p", "i=99"]);
        }
        Assert.Equal(2, written.Count);
        Assert.Contains("a=d,d=I,i=77", written[1]);
        Assert.DoesNotContain("i=99", written[1]);
    }

    [Theory]
    [InlineData("\u001b_Gi=42;ENOENT:missing\u001b\\", false)]
    [InlineData("\u001b_Gi=43;OK\u001b\\", true)]
    [InlineData("", true)]
    public void FailedOrMismatchedRepliesDoNotAcquireOwnership(string reply, bool throws) {
        var written = new List<string>();
        using var session = new KittySession(written.Add, (_, _) => reply);
        if (throws) Assert.Throws<IOException>(() => session.Execute(["a=t", "i=42", "f=100"], [1]));
        else Assert.False(session.Execute(["a=t", "i=42", "f=100"], [1]).Success);
        Assert.Empty(session.OwnedImageIds);
    }

    [Fact]
    public void ReleasedImagesSurviveSessionDisposal() {
        var written = new List<string>();
        var session = new KittySession(written.Add, (_, _) => "\u001b_Gi=42;OK\u001b\\");
        session.Execute(["a=t", "i=42", "f=100"], [1]);
        Assert.True(session.Release(42));
        session.Dispose();
        session.Dispose();
        Assert.Empty(written);
        Assert.Throws<ObjectDisposedException>(() => session.Send(["a=d"]));
    }

    [Fact]
    public void TemporaryResourceSurvivesEncodingAndIsRemovedOnDisposal() {
        var transfer = KittyTransfer.CreateTemporaryFile([1, 2, 3]);
        string path = transfer.Reference;
        try {
            Assert.Contains("tty-graphics-protocol", path);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
            Assert.Contains("t=t,S=3", transfer.Encode(["a=t", "f=100"]));
            Assert.True(File.Exists(path));
            Assert.Throws<ArgumentException>(() => transfer.Encode(["a=t", "f=100", "O=1"]));
        }
        finally { transfer.Dispose(); }
        Assert.False(File.Exists(path));
        transfer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => transfer.Encode(["f=100"]));
    }

    [Fact]
    public void TerminalMayDeleteTemporaryResourceBeforeClientDisposal() {
        using var transfer = KittyTransfer.CreateTemporaryFile([1]);
        using var session = new KittySession(_ => { }, (request, _) => {
            Assert.Contains("t=t", request);
            Assert.True(File.Exists(transfer.Reference));
            File.Delete(transfer.Reference);
            return "\u001b_Gi=42;OK\u001b\\";
        });
        Assert.True(session.ExecuteTransfer(["a=q", "i=42", "f=100"], transfer).Success);
        Assert.Empty(session.OwnedImageIds);
    }

    [Fact]
    public void SharedMemoryHasExactPayloadAndSurvivesEncoding() {
        using var transfer = KittyTransfer.CreateSharedMemory([17, 29, 41]);
        Assert.Contains("t=s,S=3", transfer.Encode(["f=24", "s=1", "v=1"]));
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            using var memory = MemoryMappedFile.OpenExisting(transfer.Reference);
            using var view = memory.CreateViewAccessor();
            Assert.Equal((byte)17, view.ReadByte(0));
            Assert.Equal((byte)41, view.ReadByte(2));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            Assert.Equal(new byte[] { 17, 29, 41 }, File.ReadAllBytes("/dev/shm" + transfer.Reference));
        }
    }

    [Theory]
    [InlineData("png")]
    [InlineData("webp")]
    public void AnimationPreservesNonGifTimingAndLoops(string format) {
        using var image = new Image<Rgba32>(1, 1, new Rgba32(255, 0, 0));
        image.Frames.AddFrame(image.Frames.RootFrame);
        image.Metadata.GetPngMetadata().RepeatCount = 3;
        image.Metadata.GetWebpMetadata().RepeatCount = 3;
        for (int index = 0; index < 2; index++) {
            image.Frames[index].Metadata.GetPngMetadata().FrameDelay = new Rational((uint)(index + 1), 10);
            image.Frames[index].Metadata.GetWebpMetadata().FrameDelay = (uint)((index + 1) * 100);
        }
        using var stream = new MemoryStream();
        if (format == "png") image.SaveAsPng(stream);
        else image.SaveAsWebp(stream);
        stream.Position = 0;
        string encoded = KittyImage.Encode(stream, new KittyImageOptions { Animate = true, ImageId = 42 }).Text;
        Assert.Contains("r=1,z=100", encoded);
        Assert.Contains("X=1,z=200", encoded);
        Assert.Contains("s=3,v=4", encoded);
    }
}
