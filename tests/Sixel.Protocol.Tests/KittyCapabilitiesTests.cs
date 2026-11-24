using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using Xunit;

namespace Sixel.Protocol.Tests;

public sealed class KittyCapabilitiesTests {
    [Fact]
    public void WezTermBasicSuccessDoesNotEnablePlaceholders() {
        var capabilities = KittyCapabilities.Detect(Terminals.WezTerm, "20260929", true);
        Assert.Equal(CapabilitySupport.Supported, capabilities.DirectTransmission);
        Assert.Equal(CapabilitySupport.Unsupported, capabilities.UnicodePlaceholders);
        Assert.Equal(CapabilitySupport.Unknown, capabilities.Animation);
        Assert.Throws<NotSupportedException>(() => capabilities.Validate(["a=T", "U=1"], false, false, false));
        Assert.Throws<NotSupportedException>(() => capabilities.Validate(["a=T"], false, true, false));
        capabilities.Validate(["a=T"], false, false, false);
        capabilities.Validate(["a=T", "U=1"], false, false, true);
    }

    [Theory]
    [InlineData("0.19.0", CapabilitySupport.Unsupported, CapabilitySupport.Unsupported, CapabilitySupport.Unsupported)]
    [InlineData("0.20.0", CapabilitySupport.Supported, CapabilitySupport.Unsupported, CapabilitySupport.Unsupported)]
    [InlineData("0.28.0", CapabilitySupport.Supported, CapabilitySupport.Supported, CapabilitySupport.Unsupported)]
    [InlineData("0.31.0", CapabilitySupport.Supported, CapabilitySupport.Supported, CapabilitySupport.Supported)]
    [InlineData("0.31.0-dev", CapabilitySupport.Supported, CapabilitySupport.Supported, CapabilitySupport.Supported)]
    [InlineData(null, CapabilitySupport.Unknown, CapabilitySupport.Unknown, CapabilitySupport.Unknown)]
    public void KittyExtensionsHaveIndependentVersionThresholds(string? version, CapabilitySupport animation, CapabilitySupport placeholders, CapabilitySupport relative) {
        var capabilities = KittyCapabilities.Detect(Terminals.Kitty, version, true);
        Assert.Equal(animation, capabilities.Animation);
        Assert.Equal(placeholders, capabilities.UnicodePlaceholders);
        Assert.Equal(relative, capabilities.RelativePlacements);
    }

    [Theory]
    [InlineData("a=f,i=1")]
    [InlineData("a=a,i=1")]
    [InlineData("a=c,i=1")]
    [InlineData("a=p,i=1,P=2")]
    [InlineData("a=t,t=s")]
    [InlineData("a=d,d=f,i=1")]
    public void UnknownExtensionsRequireAnExplicitOverride(string command) {
        var capabilities = KittyCapabilities.Detect(Terminals.unknown, null, true);
        Assert.Throws<NotSupportedException>(() => capabilities.Validate(command.Split(','), false, false, false));
        capabilities.Validate(command.Split(','), false, false, true);
    }

    private static Dictionary<uint, KittyResponse> Replies(params string[] bodies) {
        var replies = new Dictionary<uint, KittyResponse>();
        foreach (string body in bodies) {
            Assert.True(KittyResponse.TryParse("\u001b_G" + body + "\u001b\\", out KittyResponse? reply));
            replies.Add(reply!.ImageId, reply);
        }
        return replies;
    }

    // Contour master (9b2573486): parses but ignores crop, has no relative placements, echoes only nonzero keys.
    private static readonly string[] ContourMaster = [
        "i=32,e=p,p=1,c=1,r=1,C=1,U=1,z=1;OK", "i=33,e=p;OK", "i=34,e=T,o=z,f=100,s=1,v=1,U=1;OK",
        "i=35,e=a;OK", "i=36,e=f;OK", "i=37,e=c;OK"];

    [Fact]
    public void ContourMasterFeatureRepliesMapToCapabilities() {
        var capabilities = KittyCapabilities.Detect(Terminals.Contour, "0.7.0-main", true, Replies(ContourMaster));
        Assert.Equal(CapabilitySupport.Supported, capabilities.UnicodePlaceholders);
        Assert.Equal(CapabilitySupport.Unsupported, capabilities.SourceRectangle);
        Assert.Equal(CapabilitySupport.Unsupported, capabilities.RelativePlacements);
        Assert.Equal(CapabilitySupport.Supported, capabilities.Animation);
        Assert.Equal(CapabilitySupport.Supported, capabilities.FrameComposition);
        Assert.Contains("e=p{p,c,r,C,U,z}", capabilities.Evidence);
        capabilities.Validate(["a=T", "U=1"], false, true, false);
        capabilities.Validate(["a=a", "i=1", "s=3"], false, false, false);
        Assert.Throws<NotSupportedException>(() => capabilities.Validate(["a=p", "i=1", "w=10", "h=10"], false, false, false));
        Assert.Throws<NotSupportedException>(() => capabilities.Validate(["a=p", "i=1", "P=2"], false, false, false));
        capabilities.Validate(["a=p", "i=1", "w=10", "P=2"], false, false, true);
        // Frame and composition rectangles are not placement crops.
        capabilities.Validate(["a=c", "i=1", "w=1", "h=1"], false, false, false);
    }

    [Fact]
    public void FullFeatureRepliesEnableCropAndRelativePlacements() {
        var capabilities = KittyCapabilities.Detect(Terminals.Contour, null, true, Replies(
            "i=32,e=p,x=1,y=1,w=1,h=1,c=1,U=1;OK", "i=33,e=p,P=1,Q=1,H=1,V=1;OK"));
        Assert.Equal(CapabilitySupport.Supported, capabilities.SourceRectangle);
        Assert.Equal(CapabilitySupport.Supported, capabilities.RelativePlacements);
        capabilities.Validate(["a=p", "i=1", "x=2", "w=10", "P=2"], false, false, false);
    }

    [Fact]
    public void FeatureRepliesOverrideTheVersionTable() {
        var capabilities = KittyCapabilities.Detect(Terminals.Kitty, "0.31.0", true, Replies("i=32,e=p,c=1;OK", "i=33,e=p,P=1;OK"));
        Assert.Equal(CapabilitySupport.Unsupported, capabilities.UnicodePlaceholders);
        Assert.Equal(CapabilitySupport.Unsupported, capabilities.SourceRectangle);
        Assert.Equal(CapabilitySupport.Unsupported, capabilities.RelativePlacements);
        // Not asked or not answered with e=: the table stands.
        Assert.Equal(CapabilitySupport.Supported, capabilities.Animation);
    }

    [Fact]
    public void PlaceholdersFromEitherQueryCount() {
        var capabilities = KittyCapabilities.Detect(Terminals.unknown, null, true, Replies("i=32,e=p,c=1;OK", "i=34,e=T,U=1;OK"));
        Assert.Equal(CapabilitySupport.Supported, capabilities.UnicodePlaceholders);
    }

    [Theory]
    [InlineData("i=32;OK")]
    [InlineData("i=32;EINVAL:unknown key")]
    [InlineData("i=32,e=p,U=1;EINVAL")]
    [InlineData("i=32,e=T,U=1;OK")]
    [InlineData("i=35,e=a;OK")]
    public void RepliesWithoutMatchingEvidenceChangeNothing(string body) {
        foreach ((Terminals terminal, string? version) in new[] { (Terminals.Kitty, "0.31.0"), (Terminals.WezTerm, "20260929"), (Terminals.Ghostty, null), (Terminals.unknown, null) }) {
            KittyCapabilities table = KittyCapabilities.Detect(terminal, version, true);
            KittyCapabilities answered = KittyCapabilities.Detect(terminal, version, true, Replies(body));
            Assert.Equal(table.UnicodePlaceholders, answered.UnicodePlaceholders);
            Assert.Equal(table.SourceRectangle, answered.SourceRectangle);
            Assert.Equal(table.RelativePlacements, answered.RelativePlacements);
            // e=a alone is not enough for native animation, which also needs e=f.
            Assert.Equal(table.Animation, answered.Animation);
            Assert.Equal(table.FrameComposition, answered.FrameComposition);
        }
    }

    [Fact]
    public void UnknownCropIsAllowed() {
        var capabilities = KittyCapabilities.Detect(Terminals.unknown, null, true);
        Assert.Equal(CapabilitySupport.Unknown, capabilities.SourceRectangle);
        capabilities.Validate(["a=p", "i=1", "x=1", "y=1", "w=1", "h=1"], false, false, false);
    }

    [Fact]
    public void QueriesAreAllowedEvenAfterNegativeDetection() {
        var capabilities = KittyCapabilities.Detect(Terminals.Kitty, "0.40.0", false);
        capabilities.Validate(["a=q", "t=s"], false, false, false);
        Assert.Throws<NotSupportedException>(() => capabilities.Validate(["a=T"], false, false, false));
    }
}
