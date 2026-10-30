using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Sixel.Terminal.Models;


namespace Sixel.Terminal;

/// <summary>
/// Provides methods and cached properties for detecting terminal compatibility, supported protocols, and cell/window sizes.
/// </summary>
public static partial class Compatibility {
    private static readonly object s_controlSequenceLock = new();
    internal static Func<IQueryTransport> CreateQueryTransport { get; set; } = () => new ConsoleQueryTransport();
    internal static bool _runtimeProbeAttempted;
    internal static string? _terminalIdentity;

    /// <summary>Whether a runtime probe has been attempted. Skipped/redirected calls do not count.</summary>
    public static bool RuntimeProbeAttempted => _runtimeProbeAttempted;



    /// <summary>
    /// Memory-caches the result of the terminal supporting sixel graphics.
    /// </summary>
    internal static bool? _terminalSupportsSixel;

    /// <summary>
    /// Check if the terminal supports kitty graphics
    /// </summary>
    internal static bool? _terminalSupportsKitty;

    /// <summary>
    /// Memory-caches the result of the terminal cell size.
    /// </summary>
    private static CellSize? _cellSize;

    private static int? _lastWindowWidth;
    private static int? _lastWindowHeight;

    /// <summary>
    /// get the terminal info
    /// </summary>
    private static TerminalInfo? _terminalInfo;

    /// <summary>
    /// Explicitly queries the terminal. The caller must own console input (not a
    /// line editor, completion callback, or background runspace). Automatic
    /// capability and size getters never call this method.
    /// </summary>
    public static string GetControlSequenceResponse(string controlSequence) {
        lock (s_controlSequenceLock) {
            return TerminalQuery.Read(controlSequence, CreateQueryTransport());
        }
    }

    /// <summary>
    /// Explicitly refreshes capabilities and cell metrics while the caller owns
    /// console input. Do not call from module import, a prompt, or tab completion.
    /// </summary>
    public static TerminalInfo RefreshTerminalInfo() {
        lock (s_controlSequenceLock) {
            ProbeRuntime();
            return GetTerminalInfo();
        }
    }

    internal static void EnsureRuntimeProbe() {
        lock (s_controlSequenceLock) {
            if (!_runtimeProbeAttempted) ProbeRuntime();
        }
    }

    private static void ProbeRuntime() {
        IQueryTransport transport = CreateQueryTransport();
        try {
            if (transport.IsRedirected || transport.KeyAvailable) return;
        }
        catch (InvalidOperationException) { return; }
        catch (IOException) { return; }
        _runtimeProbeAttempted = true;
        string response = TerminalQuery.Read(TerminalQuery.RuntimeProbe, transport);
        // A timeout is not a negative capability result. Preserve prior evidence.
        if (response.Length == 0) return;
        _terminalSupportsKitty = false;
        _terminalSupportsSixel = false;
        int pixelWidth = 0, pixelHeight = 0, columns = 0, rows = 0;
        CellSize? measured = null;
        foreach (string segment in response.Split('\x1b')) {
            string report = "\x1b" + segment;
            if (segment.StartsWith("_G", StringComparison.Ordinal) &&
                TerminalQuery.IsKittyQueryResponse(report + "\x1b\\", out bool kittySuccess))
                _terminalSupportsKitty = kittySuccess;
            else if (segment.StartsWith("[?", StringComparison.Ordinal) && segment[segment.Length - 1] == 'c')
                _terminalSupportsSixel = segment.Substring(2, segment.Length - 3).Split(';').Skip(1).Contains("4");
            else if (TryParseSizeReport(report, 6, out int width, out int height))
                measured = new CellSize { PixelWidth = width, PixelHeight = height };
            else if (TryParseSizeReport(report, 4, out width, out height)) {
                pixelWidth = width;
                pixelHeight = height;
            }
            else if (TryParseSizeReport(report, 8, out width, out height)) {
                columns = width;
                rows = height;
            }
            else if (segment.StartsWith("P>|", StringComparison.Ordinal))
                _terminalIdentity = segment.Substring(3);
        }
        if (measured is null && columns > 0 && rows > 0 && pixelWidth >= columns && pixelHeight >= rows)
            measured = new CellSize { PixelWidth = pixelWidth / columns, PixelHeight = pixelHeight / rows };
        if (measured is not null) {
            _cellSize = measured;
            UpdateWindowSizeSnapshot();
        }
        _terminalInfo = TerminalChecker.CheckTerminal();
    }

    /// <summary>Returns a passive snapshot of independently assessed Kitty features.</summary>
    public static KittyCapabilities GetKittyCapabilities() {
        Terminals terminal = GetTerminalInfo().Terminal;
        Terminals reported = GetReportedTerminal(out string? version);
        if (reported != Terminals.unknown) terminal = reported;
        else if (Helpers.GetTerminal(Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? string.Empty) == terminal)
            version = Environment.GetEnvironmentVariable("TERM_PROGRAM_VERSION");
        return KittyCapabilities.Detect(terminal, version, _terminalSupportsKitty);
    }

    internal static Terminals GetReportedTerminal(out string? version) {
        version = null;
        if (_terminalIdentity is null) return Terminals.unknown;
        foreach (Terminals terminal in new[] { Terminals.Kitty, Terminals.WezTerm, Terminals.Ghostty, Terminals.Contour }) {
            string name = terminal.ToString();
            if (!_terminalIdentity.StartsWith(name + "(", StringComparison.OrdinalIgnoreCase) &&
                !_terminalIdentity.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)) continue;
            version = _terminalIdentity.Substring(name.Length).Trim('(', ')', ' ');
            return terminal;
        }
        return Terminals.unknown;
    }

    /// <summary>Sets known cell metrics without writing queries or reading console input.</summary>
    public static void SetCellSize(int pixelWidth, int pixelHeight) {
        if (!IsValidCellSize(pixelWidth, pixelHeight))
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Cell dimensions must be positive.");
        _cellSize = new CellSize { PixelWidth = pixelWidth, PixelHeight = pixelHeight };
        UpdateWindowSizeSnapshot();
    }
    /// <summary>
    /// Gets cached cell metrics, or the 10 by 20 fallback, without terminal I/O.
    /// Call RefreshTerminalInfo explicitly to query metrics, or SetCellSize to supply them.
    /// </summary>
    /// <returns>The number of pixel sixels that will fit in a single character cell.</returns>
    public static CellSize GetCellSize() {
        if (_cellSize is not null && !HasWindowSizeChanged()) {
            return _cellSize;
        }

        // Unknown metrics must not initiate terminal I/O during completion or import.
        return GetPlatformDefaultCellSize();
    }

    /// <summary>
    /// Minimal validation: only ensures positive integer values.
    /// Terminal-reported cell sizes are treated as ground truth.
    /// </summary>
    private static bool IsValidCellSize(int width, int height)
        => width > 0 && height > 0;

    internal static bool TryParseSizeReport(string response, int report, out int width, out int height) {
        width = height = 0;
        string prefix = "\x1b[" + report.ToString(CultureInfo.InvariantCulture) + ";";
        if (!response.StartsWith(prefix, StringComparison.Ordinal) || response[response.Length - 1] != 't')
            return false;
        string[] parts = response.Substring(prefix.Length, response.Length - prefix.Length - 1).Split(';');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out height)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out width)
            && IsValidCellSize(width, height);
    }


    /// <summary>
    /// Returns platform-specific default cell size as fallback.
    /// </summary>
    private static CellSize GetPlatformDefaultCellSize() {
        // Common terminal default sizes by platform
        // macOS terminals (especially with Retina) often use 10x20
        // Windows Terminal: 10x20
        // Linux varies: 8x16 to 10x20

        return new CellSize {
            PixelWidth = 10,
            PixelHeight = 20
        };
    }

    private static bool HasWindowSizeChanged() {
        if (Console.IsOutputRedirected || Console.IsInputRedirected) {
            return false;
        }

        try {
            int currentWidth = Console.WindowWidth;
            int currentHeight = Console.WindowHeight;

            return _lastWindowWidth.HasValue &&
                _lastWindowHeight.HasValue &&
                (_lastWindowWidth.Value != currentWidth || _lastWindowHeight.Value != currentHeight);
        }
        catch {
            return false;
        }
    }

    private static void UpdateWindowSizeSnapshot() {
        if (Console.IsOutputRedirected || Console.IsInputRedirected) {
            return;
        }

        try {
            _lastWindowWidth = Console.WindowWidth;
            _lastWindowHeight = Console.WindowHeight;
        }
        catch {
            _lastWindowWidth = null;
            _lastWindowHeight = null;
        }
    }

    /// <summary>
    /// Checks cached or environment-derived sixel support without terminal I/O.
    /// Use RefreshTerminalInfo explicitly for active capability detection.
    /// </summary>
    /// <returns>True if the terminal supports sixel graphics, false otherwise.</returns>
    public static bool TerminalSupportsSixel() {
        if (_terminalSupportsSixel.HasValue) {
            return _terminalSupportsSixel.Value;
        }
        return GetTerminalInfo().Protocol.Contains(ImageProtocol.Sixel);
    }

    /// <summary>
    /// Checks cached or environment-derived Kitty support without terminal I/O.
    /// Use RefreshTerminalInfo explicitly for active capability detection.
    /// </summary>
    /// <returns>True if the terminal supports kitty graphics, false otherwise.</returns>
    public static bool TerminalSupportsKitty() {
        if (_terminalSupportsKitty.HasValue) {
            return _terminalSupportsKitty.Value;
        }
        return GetTerminalInfo().Protocol.Contains(ImageProtocol.KittyGraphicsProtocol);
    }

    /// <summary>
    /// Get the terminal info
    /// </summary>
    /// <returns>The terminal protocol</returns>
    public static TerminalInfo GetTerminalInfo() {
        if (_terminalInfo is not null) {
            return _terminalInfo;
        }
        _terminalInfo = TerminalChecker.CheckTerminal();
        return _terminalInfo;
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"^data:image/\w+;base64,", RegexOptions.IgnoreCase, 1000)]
    internal static partial Regex Base64Image();
#else
    internal static Regex Base64Image() =>
        new(@"^data:image/\w+;base64,", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
#endif
    internal static string TrimBase64(string b64)
        => Base64Image().Replace(b64, string.Empty);

}
