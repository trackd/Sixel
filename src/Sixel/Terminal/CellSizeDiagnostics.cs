using System.Globalization;
using System.Text;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;
#pragma warning disable CA1305

/// <summary>
/// Diagnostic utilities for cell size detection and debugging.
/// </summary>
public static class CellSizeDiagnostics {
    /// <summary>
    /// Gets detailed diagnostic information about cell size detection.
    /// Useful for debugging terminal compatibility issues.
    /// </summary>
    /// <returns>Diagnostic report including raw response and parsed values.</returns>
    // CA1305 suppressed: debug diagnostic output doesn't require locale-specific formatting
    public static string GetDiagnostics() {
        StringBuilder sb = new();
        sb.AppendLine("=== Cell Size Diagnostics ===").AppendLine();

        // Test the raw response
        string response = Compatibility.GetControlSequenceResponse("[16t");

        sb
        .AppendLine("Raw Response: " + EscapeString(response))
        .AppendLine("Response Length: " + response.Length)
        .AppendLine();

        // Try parsing
        try {
            string[] parts = response.Split(';', 't');
            sb.AppendLine("Split Parts Count: " + parts.Length);
            for (int i = 0; i < parts.Length; i++) {
                sb.AppendLine("  parts[" + i + "]: '" + parts[i] + "'");
            }
            sb.AppendLine();

            if (parts.Length >= 3) {
                sb.AppendLine("Parsed PixelHeight (parts[1]): " + parts[1]);
                sb.AppendLine("Parsed PixelWidth (parts[2]): " + parts[2]);
            }
        }
        catch (Exception ex) {
            sb.AppendLine("Parse Error: " + ex.Message);
        }

        sb.AppendLine();

        // Get actual cached value
        CellSize cellSize = Compatibility.GetCellSize();
        sb.AppendLine("Final CellSize.PixelWidth: " + cellSize.PixelWidth);
        sb.AppendLine("Final CellSize.PixelHeight: " + cellSize.PixelHeight);

        return sb.ToString();
    }
#pragma warning restore CA1305

    private static string EscapeString(string input) {
        if (string.IsNullOrEmpty(input)) {
            return "(empty)";
        }

        var sb = new StringBuilder();
        foreach (char c in input) {
            // CA1305 suppressed: debug diagnostic output doesn't require locale-specific formatting
#pragma warning disable CA1305
            _ = c switch {
                '\x1b' => sb.Append("\\x1b"),
                '\r' => sb.Append("\\r"),
                '\n' => sb.Append("\\n"),
                '\t' => sb.Append("\\t"),
                '[' or ']' or ';' => sb.Append(c),
                _ => c is < (char)32 or > (char)126 ? sb.Append($"\\x{(int)c:X2}") : sb.Append(c),
            };
#pragma warning restore CA1305
        }
        return sb.ToString();
    }
}
