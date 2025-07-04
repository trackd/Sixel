using Sixel.Terminal.Models;
using System.Globalization;

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
    char? c;
    var response = string.Empty;

    Console.Write($"{Constants.ESC}{controlSequence}");
    do
    {
      c = Console.ReadKey(true).KeyChar;
      response += c;
    } while (c != 'c' && Console.KeyAvailable);
    return response;
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
      _cellSize = new CellSize
      {
        PixelWidth = int.Parse(parts[2],NumberStyles.Number,
          CultureInfo.InvariantCulture),
        PixelHeight = int.Parse(parts[1],NumberStyles.Number,
          CultureInfo.InvariantCulture)
      };
    }
    catch
    {
      // Return the default Windows Terminal size if we can't get the size from the terminal.
      _cellSize = new CellSize
      {
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
}
