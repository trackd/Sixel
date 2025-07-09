using System.Diagnostics;
using System.Globalization;
using System.Text;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;

/// <summary>
/// Provides methods and cached properties for detecting terminal compatibility, supported protocols, and cell/window sizes.
/// </summary>
public static class Compatibility
{
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
  /// Get the response to a control sequence.
  /// </summary>
  public static string GetControlSequenceResponse(string controlSequence)
  {
    if (Console.IsOutputRedirected)
    {
      return string.Empty;
    }

    var response = new StringBuilder();
    const int timeoutMs = 1000;
    // Send the control sequence
    Console.Write($"{Constants.ESC}{controlSequence}");
    var stopwatch = Stopwatch.StartNew();

    while (stopwatch.ElapsedMilliseconds < timeoutMs)
    {
      if (!Console.KeyAvailable)
      {
        Thread.Yield();
        continue;
      }

      var keyInfo = Console.ReadKey(true);
      char key = keyInfo.KeyChar;
      response.Append(key);

      // Check if we have a complete response
      if (IsCompleteResponse(response))
      {
        break;
      }
    }

    stopwatch.Stop();
    return response.ToString();
  }

  /// <summary>
  /// Check for complete terminal responses
  /// </summary>
  private static bool IsCompleteResponse(StringBuilder response)
  {
    int length = response.Length;
    if (length < 2) return false;

    // Look for common terminal response endings
    char lastChar = response[length - 1];

    // Most VT terminal responses end with specific letters
    switch (lastChar)
    {
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
          for (int i = 0; i <= length - 3; i++)
          {
            if (response[i] == ';' && i + 2 < length &&
                response[i + 1] == 'O' && response[i + 2] == 'K')
            {
              hasOK = true;
              break;
            }
          }

          if (hasOK)
          {
            // Look for ESC\ (String Terminator)
            int stIndex = -1;
            for (int i = 0; i < length - 1; i++)
            {
              if (response[i] == '\x1b' && response[i + 1] == '\\')
              {
                stIndex = i;
                break;
              }
            }

            if (stIndex >= 0 && stIndex + 2 < length)
            {
              // Check if there's a complete response after the ST
              int afterSTStart = stIndex + 2;
              int afterSTLength = length - afterSTStart;
              if (afterSTLength >= 3 &&
                  response[afterSTStart] == '\x1b' &&
                  response[afterSTStart + 1] == '[')
              {
                char afterSTLast = response[length - 1];
                return afterSTLast == 'c' ||
                      afterSTLast == 'R' ||
                      afterSTLast == 't' ||
                      afterSTLast == 'n' ||
                      afterSTLast == 'y';
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
  public static CellSize GetCellSize()
  {
    if (_cellSize is not null)
    {
      return _cellSize;
    }

    var response = GetControlSequenceResponse("[16t");

    try
    {
      var parts = response.Split(';', 't');
      _cellSize = new CellSize {
        PixelWidth = int.Parse(parts[2], NumberStyles.Number,
          CultureInfo.InvariantCulture),
        PixelHeight = int.Parse(parts[1], NumberStyles.Number,
          CultureInfo.InvariantCulture)
      };
    }
    catch
    {
      // Return the default Windows Terminal size if we can't get the size from the terminal.
      _cellSize = new CellSize {
        PixelWidth = 10,
        PixelHeight = 20
      };
    }
    return _cellSize;
  }

  /// <summary>
  /// Check if the terminal supports sixel graphics.
  /// This is done by sending the terminal a Device Attributes request.
  /// If the terminal responds with a response that contains ";4;" then it supports sixel graphics.
  /// https://vt100.net/docs/vt510-rm/DA1.html
  /// </summary>
  /// <returns>True if the terminal supports sixel graphics, false otherwise.</returns>
  public static bool TerminalSupportsSixel()
  {
    if (_terminalSupportsSixel.HasValue)
    {
      return _terminalSupportsSixel.Value;
    }
    var response = GetControlSequenceResponse("[c");
    _terminalSupportsSixel = response.Contains(";4;") || response.Contains(";4c");
    return _terminalSupportsSixel.Value;
  }

  /// <summary>
  /// Check if the terminal supports kitty graphics.
  /// https://sw.kovidgoyal.net/kitty/graphics-protocol/
  /// response: ␛_Gi=31;OK␛\␛[?62;c
  /// </summary>
  /// <returns>True if the terminal supports sixel graphics, false otherwise.</returns>
  public static bool TerminalSupportsKitty()
  {
    if (_terminalSupportsKitty.HasValue)
    {
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
  public static TerminalInfo GetTerminalInfo()
  {
    if (_terminalInfo != null)
    {
      return _terminalInfo;
    }

    _terminalInfo = TerminalChecker.CheckTerminal();
    return _terminalInfo;
  }

  /// <summary>
  /// Gets the current cursor position using Console.GetCursorPosition() when available,
  /// or falls back to ANSI escape sequences on older .NET versions.
  /// </summary>
  /// <returns>A tuple containing (row, column) of the cursor position, or null if query failed.</returns>
  public static (int Row, int Column)? GetCursorPosition()
  {
    try
    {
#if NET472
      // .NET Framework doesn't have Console.GetCursorPosition(), use ANSI escape sequences
      var response = GetControlSequenceResponse("[6n");

      // Response format: ESC[row;columnR
      // Remove the ESC[ prefix and R suffix, then parse
      if (response.StartsWith("[", StringComparison.Ordinal) && response.EndsWith("R", StringComparison.Ordinal))
      {
        var coords = response.Substring(1, response.Length - 2); // Remove [ and R
        var parts = coords.Split(';');

        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int row) &&
            int.TryParse(parts[1], out int column))
        {
          return (row, column);
        }
      }
#else
      // .NET Core/.NET 5+ has Console.GetCursorPosition()
      var (left, top) = Console.GetCursorPosition();
      return (top, left); // Convert to (row, column) format
#endif
    }
    catch
    {
      // If anything goes wrong, return null to fall back to legacy behavior
    }

    return null;
  }

  /// <summary>
  /// Measures the actual height of rendered content by comparing cursor positions
  /// before and after rendering. This provides a dynamic, terminal-agnostic way
  /// to determine how many rows an image actually occupies.
  /// </summary>
  /// <param name="renderAction">The action that renders content to the terminal.</param>
  /// <returns>The number of rows the content actually occupied, or null if measurement failed.</returns>
  public static int? MeasureRenderedHeight(Action renderAction)
  {
    if (renderAction is null) return null;

    try
    {
      // Get cursor position before rendering
      var beforePos = GetCursorPosition();
      if (!beforePos.HasValue) return null;

      // Render the content
      renderAction();

      // Get cursor position after rendering
      var afterPos = GetCursorPosition();
      if (!afterPos.HasValue) return null;

      // Calculate the height difference
      int heightDiff = afterPos.Value.Row - beforePos.Value.Row;

      // Return the measured height (ensure it's non-negative)
      return Math.Max(0, heightDiff);
    }
    catch
    {
      // If measurement fails, return null to fall back to legacy behavior
      return null;
    }
  }
}
