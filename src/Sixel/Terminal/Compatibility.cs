using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;

/// <summary>
/// Provides methods and cached properties for detecting terminal compatibility, supported protocols, and cell/window sizes.
/// </summary>
public static class Compatibility {
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

    /// <summary>
    /// get the terminal info
    /// </summary>
    private static TerminalInfo? _terminalInfo;

    /// <summary>
    /// Resets all cached terminal compatibility values, forcing re-detection on next access.
    /// Useful when terminal settings change or for testing.
    /// </summary>
    public static void ResetCache() {
        _cellSize = null;
        _terminalSupportsSixel = null;
        _terminalSupportsKitty = null;
        _terminalInfo = null;
    }

    /// <summary>
    /// Get the response to a control sequence.
    /// Only queries when it's safe to do so (no pending input, not redirected).
    /// </summary>
    public static string GetControlSequenceResponse(string controlSequence) {
        if (Console.IsOutputRedirected || Console.IsInputRedirected) {
            return string.Empty;
        }

        try {
            var response = new StringBuilder();
            const int timeoutMs = 1000;

            // Send the control sequence
            Console.Write($"{Constants.ESC}{controlSequence}");
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs) {
                if (!Console.KeyAvailable) {
                    Thread.Sleep(1); // Small sleep instead of Yield for more predictable timing
                    continue;
                }

                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                char key = keyInfo.KeyChar;
                response.Append(key);

                // Check if we have a complete response
                if (IsCompleteResponse(response)) {
                    break;
                }
            }

            return response.ToString();
        }
        catch (Exception) {
            return string.Empty;
        }
    }


    /// <summary>
    /// Check for complete terminal responses
    /// </summary>
    private static bool IsCompleteResponse(StringBuilder response) {
        int length = response.Length;
        if (length < 2) return false;

        // Look for common terminal response endings
        char lastChar = response[length - 1];

        // Most VT terminal responses end with specific letters
        switch (lastChar) {
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
        if (_cellSize is not null) {
            return _cellSize;
        }

        // Check for environment variable override first
        string? envWidth = Environment.GetEnvironmentVariable("SIXEL_CELL_WIDTH");
        string? envHeight = Environment.GetEnvironmentVariable("SIXEL_CELL_HEIGHT");
        if (!string.IsNullOrEmpty(envWidth) && !string.IsNullOrEmpty(envHeight) &&
            int.TryParse(envWidth, out int overrideWidth) && int.TryParse(envHeight, out int overrideHeight) &&
            IsValidCellSize(overrideWidth, overrideHeight)) {
            _cellSize = new CellSize {
                PixelWidth = overrideWidth,
                PixelHeight = overrideHeight
            };
            return _cellSize;
        }

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
                    return _cellSize;
                }
            }
        }
        catch {
            // Fall through to platform-specific fallback
        }

        // Platform-specific fallback values
        _cellSize = GetPlatformDefaultCellSize();
        return _cellSize;
    }

    /// <summary>
    /// Validates that cell size values are within reasonable bounds.
    /// Detects obviously wrong values like those from buggy terminal responses.
    /// </summary>
    private static bool IsValidCellSize(int width, int height) {
        // Cell sizes should be:
        // - Positive
        // - Reasonably sized (typical range: 6-20 width, 10-32 height)
        // - Height should generally be >= width (cells are usually taller than wide)
        // - Not suspiciously small (< 5 indicates likely parse error or DPI issue)
        // - Not suspiciously large (> 30 indicates likely DPI scaling issue)

        if (width <= 0 || height <= 0) return false;
        if (width is < 5 or > 30) return false;
        if (height is < 10 or > 40) return false;

        // Reject obviously wrong aspect ratios (e.g., 6x4 from WezTerm on Mac)
        // Normal cells are roughly 1:2 to 1:3 ratio (width:height)
        double ratio = (double)height / width;
        return ratio is not (< 1.0 or > 4.0);
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
        if (_terminalInfo != null) {
            return _terminalInfo;
        }

        _terminalInfo = TerminalChecker.CheckTerminal();
        return _terminalInfo;
    }

}
