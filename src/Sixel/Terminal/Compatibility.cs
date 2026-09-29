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
            string response = GetControlSequenceResponse(TerminalQuery.KittyAndDeviceAttributes);
            if (response.Length > 0) {
                _terminalSupportsKitty = response.Contains(";OK");
                _terminalSupportsSixel = response.Contains(";4;") || response.Contains(";4c");
            }
            QueryCellSize();
            _terminalInfo = TerminalChecker.CheckTerminal();
            return _terminalInfo;
        }
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

    private static CellSize QueryCellSize() {
        string response = GetControlSequenceResponse("[16t");

        if (TryParseSizeReport(response, 6, out int width, out int height)) {
            _cellSize = new CellSize { PixelWidth = width, PixelHeight = height };
            UpdateWindowSizeSnapshot();
            return _cellSize;
        }

        // Some terminals implement window pixel/cell queries but not CSI 16 t.
        if (TryParseSizeReport(GetControlSequenceResponse("[14t"), 4, out int pixelWidth, out int pixelHeight)
            && TryParseSizeReport(GetControlSequenceResponse("[18t"), 8, out int columns, out int rows)
            && pixelWidth >= columns && pixelHeight >= rows) {
            _cellSize = new CellSize { PixelWidth = pixelWidth / columns, PixelHeight = pixelHeight / rows };
            UpdateWindowSizeSnapshot();
            return _cellSize;
        }

        // Platform-specific fallback values
        _cellSize ??= GetPlatformDefaultCellSize();
        UpdateWindowSizeSnapshot();
        return _cellSize;
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
