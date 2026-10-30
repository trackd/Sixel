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

    [Fact]
    public void QueriesAreAllowedEvenAfterNegativeDetection() {
        var capabilities = KittyCapabilities.Detect(Terminals.Kitty, "0.40.0", false);
        capabilities.Validate(["a=q", "t=s"], false, false, false);
        Assert.Throws<NotSupportedException>(() => capabilities.Validate(["a=T"], false, false, false));
    }
}
