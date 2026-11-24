using System.Reflection;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using Xunit;

namespace Sixel.Protocol.Tests;

public sealed class TerminalQueryTests : IDisposable {
    private readonly Dictionary<FieldInfo, object?> saved = [];
    private readonly Func<IQueryTransport> originalFactory = Compatibility.CreateQueryTransport;

    public TerminalQueryTests() {
        foreach (string name in new[] { "_cellSize", "_terminalInfo", "_terminalSupportsKitty", "_terminalSupportsSixel", "_lastWindowWidth", "_lastWindowHeight" }) {
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
        // Passive fallbacks must not cache negative probe results.
        Assert.Null(Compatibility._terminalSupportsKitty);
        Assert.Null(Compatibility._terminalSupportsSixel);
    }

    [Fact]
    public void ExplicitRefreshUpdatesCapabilitiesAndMetrics() {
        var transports = new Queue<FakeTransport>([
            new("\x1b_Gi=31;OK\x1b\\\x1b[?62;4c"), new("\x1b[6;18;9t")
        ]);
        Compatibility.CreateQueryTransport = transports.Dequeue;
        TerminalInfo info = Compatibility.RefreshTerminalInfo();
        Assert.Contains(ImageProtocol.KittyGraphicsProtocol, info.Protocol);
        Assert.Contains(ImageProtocol.Sixel, info.Protocol);
        Assert.Equal(9, Compatibility.GetCellSize().PixelWidth);
        Assert.Equal(18, Compatibility.GetCellSize().PixelHeight);
        Assert.Empty(transports);
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
        Assert.Equal(500, terminal.ElapsedMilliseconds);
    }

    [Fact]
    public void MetricsQueryMatchesReportTypeAndStopsAtItsTerminator() {
        var terminal = new FakeTransport("\x1b[?62;4c\x1b[4;800;1200t\x1b[6;20;10tinput");
        Assert.Equal("\x1b[6;20;10t", TerminalQuery.Read("[16t", terminal));
        Assert.Equal("input", terminal.Remaining);
    }

    private sealed class FakeTransport(string response) : IQueryTransport {
        private readonly Queue<(char Character, long At)> input = new();
        internal List<string> Writes { get; } = [];
        internal int Reads { get; private set; }
        internal Action? OnWrite { get; set; }
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
}
