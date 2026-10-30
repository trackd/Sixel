using System.Reflection;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using Sixel.Cmdlet;
using Xunit;

namespace Sixel.Protocol.Tests;

public sealed class TerminalQueryTests : IDisposable {
    private readonly Dictionary<FieldInfo, object?> saved = [];
    private readonly Func<IQueryTransport> originalFactory = Compatibility.CreateQueryTransport;

    public TerminalQueryTests() {
        foreach (string name in new[] { "_cellSize", "_terminalInfo", "_terminalSupportsKitty", "_terminalSupportsSixel", "_lastWindowWidth", "_lastWindowHeight", "_runtimeProbeAttempted", "_terminalIdentity" }) {
            FieldInfo field = typeof(Compatibility).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!;
            saved.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
    }

    public void Dispose() {
        Compatibility.CreateQueryTransport = originalFactory;
        foreach (KeyValuePair<FieldInfo, object?> entry in saved) entry.Key.SetValue(null, entry.Value);
    }

    [Fact]
    public void AutomaticDiscoveryNeverOpensQueryTransport() {
        Compatibility.CreateQueryTransport = () => throw new InvalidOperationException("Automatic terminal I/O");
        Assert.NotNull(Compatibility.GetTerminalInfo());
        _ = Compatibility.TerminalSupportsKitty();
        _ = Compatibility.TerminalSupportsSixel();
        Assert.Equal(10, Compatibility.GetCellSize().PixelWidth);
        Assert.Equal(20, Compatibility.GetCellSize().PixelHeight);
        Assert.NotNull(TerminalChecker.CheckTerminal());
        Assert.NotNull(Compatibility.GetKittyCapabilities());
        // Passive fallbacks must not cache negative probe results.
        Assert.Null(Compatibility._terminalSupportsKitty);
        Assert.Null(Compatibility._terminalSupportsSixel);
    }

    [Theory]
    [InlineData("\x1b_Gi=31;OK\x1b\\\x1b[?65;1;3;4;7;9;18;21;22;29;52;314c")]
    [InlineData("")]
    public void QueryAcquiresInputModeBeforeWritingAndRestoresIt(string response) {
        var terminal = new FakeTransport(response);
        terminal.OnWrite = () => Assert.True(terminal.ModeActive);
        TerminalQuery.Read(TerminalQuery.KittyAndDeviceAttributes, terminal);
        Assert.False(terminal.ModeActive);
        Assert.Equal(1, terminal.ModeEntries);
    }

    [Fact]
    public void InputModeIsRestoredWhenWritingFails() {
        var terminal = new FakeTransport("") { OnWrite = () => throw new IOException("Write failed") };
        Assert.Empty(TerminalQuery.Read(TerminalQuery.RuntimeProbe, terminal));
        Assert.False(terminal.ModeActive);
    }

    [Fact]
    public void ContourIdentityAndKittyAcknowledgementAreRecognizedSeparately() {
        Compatibility.CreateQueryTransport = () => new FakeTransport(
            "\x1b_Gi=31;OK\x1b\\\x1bP>|contour 0.7.0-main\x1b\\\x1b[?65;1;3;4;7;9;18;21;22;29;52;314c");
        TerminalInfo info = Compatibility.RefreshTerminalInfo();
        Assert.Equal(Terminals.Contour, info.Terminal);
        Assert.Contains(ImageProtocol.KittyGraphicsProtocol, info.Protocol);
        Assert.Equal("0.7.0-main", Compatibility.GetKittyCapabilities().Version);
    }

    [Fact]
    public void ContourDaExtensionsDoNotImplyKittySupport() {
        Compatibility.CreateQueryTransport = () => new FakeTransport("\x1b[?65;1;3;4;7;9;18;21;22;29;52;314c");
        Compatibility.RefreshTerminalInfo();
        Assert.True(Compatibility._terminalSupportsSixel);
        Assert.False(Compatibility._terminalSupportsKitty);
    }

    [Fact]
    public void SizeReportNumberFourDoesNotAdvertiseSixel() {
        Compatibility.CreateQueryTransport = () => new FakeTransport("\x1b[8;4;80t\x1b[?62;1c");
        Compatibility.RefreshTerminalInfo();
        Assert.False(Compatibility._terminalSupportsSixel);
    }

    [Fact]
    public void KittyReplyMayIncludePlacementIdentifier() {
        Compatibility.CreateQueryTransport = () => new FakeTransport("\x1b_Gp=0,i=31;OK\x1b\\\x1b[?62;1c");
        Compatibility.RefreshTerminalInfo();
        Assert.True(Compatibility._terminalSupportsKitty);
    }

    [Fact]
    public void ExplicitRefreshUpdatesCapabilitiesAndMetrics() {
        var transports = new Queue<FakeTransport>([
            new("\x1b_Gi=31;OK\x1b\\\x1bP>|kitty(0.31.0)\x1b\\\x1b[6;18;9t\x1b[?62;4c")
        ]);
        Compatibility.CreateQueryTransport = transports.Dequeue;
        TerminalInfo info = Compatibility.RefreshTerminalInfo();
        Assert.Contains(ImageProtocol.KittyGraphicsProtocol, info.Protocol);
        Assert.Contains(ImageProtocol.Sixel, info.Protocol);
        Assert.Equal(9, Compatibility.GetCellSize().PixelWidth);
        Assert.Equal(18, Compatibility.GetCellSize().PixelHeight);
        Assert.Empty(transports);
        Assert.Equal(CapabilitySupport.Supported, Compatibility.GetKittyCapabilities().UnicodePlaceholders);
        Assert.Equal("0.31.0", Compatibility.GetKittyCapabilities().Version);
    }

    [Fact]
    public void ExplicitMetricsNeedNoQueriesAndSurviveFailedRefresh() {
        Compatibility.SetCellSize(8, 16);
        Compatibility.CreateQueryTransport = () => new FakeTransport("") { IsRedirected = true };
        Compatibility.RefreshTerminalInfo();
        Assert.Equal(8, Compatibility.GetCellSize().PixelWidth);
        Assert.Equal(16, Compatibility.GetCellSize().PixelHeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => Compatibility.SetCellSize(0, 16));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RedirectedOrPendingInputPreventsQuery(bool redirected) {
        var terminal = new FakeTransport("") { IsRedirected = redirected };
        if (!redirected) terminal.Enqueue("user input", 0);
        Assert.Empty(TerminalQuery.Read("[c", terminal));
        Assert.Empty(terminal.Writes);
        Assert.Equal(0, terminal.Reads);
        if (!redirected) Assert.Equal("user input", terminal.Remaining);
    }

    [Fact]
    public void KittyQueryConsumesDelayedDaButLeavesSubsequentKeys() {
        const string kitty = "\x1b_Gi=31;OK\x1b\\";
        const string da = "\x1b[?62;4c";
        var terminal = new FakeTransport(kitty);
        terminal.OnWrite = () => {
            terminal.Enqueue(da, 100);
            terminal.Enqueue("next command", 100);
        };
        Assert.Equal(kitty + da, TerminalQuery.Read(TerminalQuery.KittyAndDeviceAttributes, terminal));
        Assert.Equal(100, terminal.ElapsedMilliseconds);
        Assert.Equal("next command", terminal.Remaining);
        Assert.Single(terminal.Writes);
    }

    [Theory]
    [InlineData("\x1b[?62;4c")]
    [InlineData("\x1b_Gi=31;EINVAL\x1b\\\x1b[?62;4c")]
    public void UnsupportedKittyStillConsumesDaFence(string response) {
        var terminal = new FakeTransport(response);
        Assert.Equal(response, TerminalQuery.Read(TerminalQuery.KittyAndDeviceAttributes, terminal));
        Assert.Empty(terminal.Remaining);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\x1b[?62;")]
    [InlineData("\x1b_Gi=31;OK\x1b\\")]
    public void TimeoutDoesNotRetryOrReturnPartialResponse(string partial) {
        var terminal = new FakeTransport(partial);
        Assert.Empty(TerminalQuery.Read(TerminalQuery.KittyAndDeviceAttributes, terminal));
        Assert.Single(terminal.Writes);
        Assert.Equal(TerminalQuery.TimeoutMilliseconds, terminal.ElapsedMilliseconds);
    }

    [Fact]
    public void MetricsQueryMatchesReportTypeAndStopsAtItsTerminator() {
        var terminal = new FakeTransport("\x1b[?62;4c\x1b[4;800;1200t\x1b[6;20;10tinput");
        Assert.Equal("\x1b[6;20;10t", TerminalQuery.Read("[16t", terminal));
        Assert.Equal("input", terminal.Remaining);
    }

    private sealed class FakeTransport(string response) : IQueryTransport, IQueryInputMode, IDisposable {
        private readonly Queue<(char Character, long At)> input = new();
        internal List<string> Writes { get; } = [];
        internal int Reads { get; private set; }
        internal Action? OnWrite { get; set; }
        internal bool ModeActive { get; private set; }
        internal int ModeEntries { get; private set; }
        public IDisposable EnterQueryMode() { ModeEntries++; ModeActive = true; return this; }
        public void Dispose() => ModeActive = false;
        internal string Remaining => new([.. input.Select(item => item.Character)]);
        public bool IsRedirected { get; init; }
        public bool KeyAvailable => input.Count > 0 && input.Peek().At <= ElapsedMilliseconds;
        public long ElapsedMilliseconds { get; private set; }
        internal void Enqueue(string text, long at) {
            foreach (char value in text) input.Enqueue((value, at));
        }
        public char Read() { Reads++; return input.Dequeue().Character; }
        public void Write(string text) { Writes.Add(text); Enqueue(response, 0); OnWrite?.Invoke(); }
        public void Wait() => ElapsedMilliseconds++;
    }

    [Fact]
    public void LazyProbeRunsOnceAndExplicitRefreshCanRepeatIt() {
        int opened = 0;
        Compatibility.CreateQueryTransport = () => {
            opened++;
            return new FakeTransport("\x1b_Gi=31;OK\x1b\\\x1b[6;24;12t\x1b[?62;4c");
        };
        Compatibility.EnsureRuntimeProbe();
        Compatibility.EnsureRuntimeProbe();
        Assert.Equal(1, opened);
        Assert.True(Compatibility.RuntimeProbeAttempted);
        Assert.Equal(12, Compatibility.GetCellSize().PixelWidth);
        Compatibility.RefreshTerminalInfo();
        Assert.Equal(2, opened);
    }

    [Fact]
    public void SkippedLazyProbeDoesNotPreventALaterEligibleAttempt() {
        var pending = new FakeTransport("");
        pending.Enqueue("typed ahead", 0);
        Compatibility.CreateQueryTransport = () => pending;
        Compatibility.EnsureRuntimeProbe();
        Assert.False(Compatibility.RuntimeProbeAttempted);
        Assert.Empty(pending.Writes);
        Assert.Equal("typed ahead", pending.Remaining);
        Compatibility.CreateQueryTransport = () => new FakeTransport("\x1b[?62;4c");
        Compatibility.EnsureRuntimeProbe();
        Assert.True(Compatibility.RuntimeProbeAttempted);
    }

    [Fact]
    public void SlowResponseWithinExtendedBudgetIsAccepted() {
        var terminal = new FakeTransport("");
        terminal.OnWrite = () => terminal.Enqueue("\x1b[?62;4c", 750);
        Assert.Equal("\x1b[?62;4c", TerminalQuery.Read(TerminalQuery.RuntimeProbe, terminal));
        Assert.Equal(750, terminal.ElapsedMilliseconds);
    }

    [Fact]
    public void TimedOutBatchIsNotRetriedAutomaticallyOrTreatedAsNegative() {
        var terminal = new FakeTransport("\x1b_Gi=31;OK\x1b\\");
        Compatibility.CreateQueryTransport = () => terminal;
        Compatibility.EnsureRuntimeProbe();
        Compatibility.EnsureRuntimeProbe();
        Assert.Single(terminal.Writes);
        Assert.Equal(TerminalQuery.TimeoutMilliseconds, terminal.ElapsedMilliseconds);
        Assert.Null(Compatibility._terminalSupportsKitty);
        Assert.Null(Compatibility._terminalSupportsSixel);
    }

    [Fact]
    public void BatchConsumesAllReportsThroughDaFenceAndLeavesFollowingInput() {
        const string reports = "\x1b_Gi=31;OK\x1b\\\x1bP>|WezTerm 20250320-1\x1b\\\x1b[6;20;10t\x1b[4;800;1200t\x1b[8;40;120t";
        var terminal = new FakeTransport(reports);
        terminal.OnWrite = () => terminal.Enqueue("\x1b[?62;4cnext command", 100);
        Assert.Equal(reports + "\x1b[?62;4c", TerminalQuery.Read(TerminalQuery.RuntimeProbe, terminal));
        Assert.Equal("next command", terminal.Remaining);
        Assert.Single(terminal.Writes);
    }

    [Fact]
    public void BatchCanDeriveCellMetricsFromWindowReports() {
        Compatibility.CreateQueryTransport = () => new FakeTransport("\x1b[4;800;1200t\x1b[8;40;120t\x1b[?62;4c");
        Compatibility.EnsureRuntimeProbe();
        Assert.Equal(10, Compatibility.GetCellSize().PixelWidth);
        Assert.Equal(20, Compatibility.GetCellSize().PixelHeight);
        Assert.False(Compatibility._terminalSupportsKitty);
    }

    [Fact]
    public void NegativeProbeOverridesInferredProtocolButKeepsFallback() {
        Compatibility._terminalIdentity = "kitty(0.31.0)";
        Compatibility._terminalSupportsKitty = false;
        Assert.DoesNotContain(ImageProtocol.KittyGraphicsProtocol, TerminalChecker.CheckTerminal().Protocol);
        Assert.Contains(ImageProtocol.Blocks, TerminalChecker.CheckTerminal().Protocol);
    }

    [Fact]
    public void CellReportTakesPrecedenceOverWindowEstimate() {
        Compatibility.CreateQueryTransport = () => new FakeTransport("\x1b[6;24;12t\x1b[4;800;1200t\x1b[8;40;120t\x1b[?62;4c");
        Compatibility.EnsureRuntimeProbe();
        Assert.Equal(12, Compatibility.GetCellSize().PixelWidth);
        Assert.Equal(24, Compatibility.GetCellSize().PixelHeight);
    }

    [Theory]
    [InlineData("ConsoleHost", "TabExpansion2", false)]
    [InlineData("ConsoleHost", "prompt", false)]
    [InlineData("ConsoleHost", "PSConsoleHostReadLine", false)]
    [InlineData("ServerRemoteHost", "ConvertTo-Kitty", false)]
    [InlineData("ConsoleHost", "ConvertTo-Kitty", true)]
    public void OnlyOrdinaryConsoleExecutionIsEligible(string host, string caller, bool expected) =>
        Assert.Equal(expected, TerminalExecution.CanProbe(host, [caller]));
}
