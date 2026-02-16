using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Sixel.Terminal.Models;


namespace Sixel.Terminal;

/// <summary>
/// Provides methods and cached properties for detecting terminal compatibility, supported protocols, and cell/window sizes.
/// </summary>
public static partial class Compatibility {
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
    /// Get the response to a control sequence.
    /// Only queries when it's safe to do so (no pending input, not redirected).
    /// Retries up to 2 times with 500ms timeout each.
    /// </summary>
    public static string GetControlSequenceResponse(string controlSequence) {
        if (Console.IsOutputRedirected || Console.IsInputRedirected) {
            return string.Empty;
        }

        const int timeoutMs = 500;
        const int maxRetries = 2;

        for (int retry = 0; retry < maxRetries; retry++) {
            try {
                var response = new StringBuilder();
                bool capturing = false;

                // Send the control sequence
                Console.Write($"{Constants.ESC}{controlSequence}");
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.ElapsedMilliseconds < timeoutMs) {
                    if (!Console.KeyAvailable) {
                        Thread.Sleep(1);
                        continue;
                    }

                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                    char key = keyInfo.KeyChar;

                    if (!capturing) {
                        if (key != '\x1b') {
                            continue;
                        }
                        capturing = true;
                    }

                    response.Append(key);

                    // Check if we have a complete response
                    if (IsCompleteResponse(response)) {
                        return response.ToString();
                    }
                }

                // If we got a partial response, return it
                if (response.Length > 0) {
                    return response.ToString();
                }
            }
            catch (Exception) {
                if (retry == maxRetries - 1) {
                    return string.Empty;
                }
            }
        }

        return string.Empty;
    }


    /// <summary>
    /// Check for complete terminal responses
    /// </summary>
    private static bool IsCompleteResponse(StringBuilder response) {
        int length = response.Length;
        if (length < 2) return false;


        // Most VT terminal responses end with specific letters
        switch (response[length - 1]) {
            case 'c': // Device Attributes (ESC[...c)
            case 'R': // Cursor Position Report (ESC[row;columnR)
            case 't': // Window manipulation (ESC[...t)
            case 'n': // Device Status Report (ESC[...n)
            case 'y': // DECRPM response (ESC[?...y)
                      // Make sure it's actually a CSI sequence (ESC[)
                return length >= 3 && response[0] == '\x1b' && response[1] == '[';

            case '\\': // String Terminator (ESC\)
                return length >= 2 && response[length - 2] == '\x1b';

            case (char)7: // BEL character
                return true;

            default:
                // Check for Kitty graphics protocol: ends with ";OK" followed by ST and then another response
                if (length >= 7) // Minimum for ";OK" + ESC\ + ESC[...c
                {
                    // Look for ";OK" pattern
                    bool hasOK = false;
                    for (int i = 0; i <= length - 3; i++) {
                        if (response[i] == ';' && i + 2 < length &&
                            response[i + 1] == 'O' && response[i + 2] == 'K') {
                            hasOK = true;
                            break;
                        }
                    }

                    if (hasOK) {
                        // Look for ESC\ (String Terminator)
                        int stIndex = -1;
                        for (int i = 0; i < length - 1; i++) {
                            if (response[i] == '\x1b' && response[i + 1] == '\\') {
                                stIndex = i;
                                break;
                            }
                        }

                        if (stIndex >= 0 && stIndex + 2 < length) {
                            // Check if there's a complete response after the ST
                            int afterSTStart = stIndex + 2;
                            int afterSTLength = length - afterSTStart;
                            if (afterSTLength >= 3 &&
                                response[afterSTStart] == '\x1b' &&
                                response[afterSTStart + 1] == '[') {
                                char afterSTLast = response[length - 1];
                                return afterSTLast is 'c' or
                                        'R' or
                                        't' or
                                        'n' or
                                        'y';
                            }
                        }
                    }
                }
                return false;
        }
    }

    /// <summary>
    /// Get the cell size of the terminal in pixel-sixel size.
    /// The response to the command will look like [6;20;10t where the 20 is height and 10 is width.
    /// I think the 6 is the terminal class, which is not used here.
    /// </summary>
    /// <returns>The number of pixel sixels that will fit in a single character cell.</returns>
    public static CellSize GetCellSize() {
        if (_cellSize is not null && !HasWindowSizeChanged()) {
            return _cellSize;
        }

        _cellSize = null;
        string response = GetControlSequenceResponse("[16t");

        try {
            string[] parts = response.Split(';', 't');
            if (parts.Length >= 3) {
                int width = int.Parse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture);
                int height = int.Parse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture);

                // Validate the parsed values are reasonable
                if (IsValidCellSize(width, height)) {
                    _cellSize = new CellSize {
                        PixelWidth = width,
                        PixelHeight = height
                    };
                    UpdateWindowSizeSnapshot();
                    return _cellSize;
                }
            }
        }
        catch {
            // Fall through to platform-specific fallback
        }

        // Platform-specific fallback values
        _cellSize = GetPlatformDefaultCellSize();
        UpdateWindowSizeSnapshot();
        return _cellSize;
    }

    /// <summary>
    /// Minimal validation: only ensures positive integer values.
    /// Terminal-reported cell sizes are treated as ground truth.
    /// </summary>
    private static bool IsValidCellSize(int width, int height)
        => width > 0 && height > 0;


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
    /// Check if the terminal supports sixel graphics.
    /// This is done by sending the terminal a Device Attributes request.
    /// If the terminal responds with a response that contains ";4;" then it supports sixel graphics.
    /// https://vt100.net/docs/vt510-rm/DA1.html
    /// </summary>
    /// <returns>True if the terminal supports sixel graphics, false otherwise.</returns>
    public static bool TerminalSupportsSixel() {
        if (_terminalSupportsSixel.HasValue) {
            return _terminalSupportsSixel.Value;
        }
        string response = GetControlSequenceResponse("[c");
        _terminalSupportsSixel = response.Contains(";4;") || response.Contains(";4c");
        return _terminalSupportsSixel.Value;
    }

    /// <summary>
    /// Check if the terminal supports kitty graphics.
    /// https://sw.kovidgoyal.net/kitty/graphics-protocol/
    /// response: ␛_Gi=31;OK␛\␛[?62;c
    /// </summary>
    /// <returns>True if the terminal supports kitty graphics, false otherwise.</returns>
    public static bool TerminalSupportsKitty() {
        if (_terminalSupportsKitty.HasValue) {
            return _terminalSupportsKitty.Value;
        }
        string kittyTest = $"_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA{Constants.ST}{Constants.ESC}[c";
        _terminalSupportsKitty = GetControlSequenceResponse(kittyTest).Contains(";OK");
        return _terminalSupportsKitty.Value;
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
