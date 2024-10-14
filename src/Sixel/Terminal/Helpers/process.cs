using Sixel.Terminal.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Sixel.Terminal.Native;

internal partial class ProcessHelper
{
  /// <summary>
  /// The pattern to match the windows terminal edition.
  /// </summary>
  private const string EditionPattern = "Preview|Canary|Dev|system32";

  /// <summary>
  /// Check the terminal of the current process.
  /// </summary>
  internal static (Terminals Terminal, ImageProtocol Protocol)? CheckParent(int id)
  {
    var parentProcess = GetParentProcess(id);
    if (parentProcess == null)
    {
      return null;
    }

    var parentFileName = parentProcess.MainModule?.FileName;
    if (string.IsNullOrEmpty(parentFileName))
    {
      return null;
    }

    var edition = Regex.Match(parentFileName, EditionPattern, RegexOptions.IgnoreCase).Value;
    return edition switch {
      "Preview" => TerminalChecker.GetTerminalProtocol(Terminals.MicrosoftTerminalPreview),
      "Canary" => TerminalChecker.GetTerminalProtocol(Terminals.MicrosoftTerminalCanary),
      "Dev" => TerminalChecker.GetTerminalProtocol(Terminals.MicrosoftTerminalDev),
      "system32" => TerminalChecker.GetTerminalProtocol(Terminals.MicrosoftConhost),
      _ => TerminalChecker.GetTerminalProtocol(Terminals.MicrosoftTerminal)
    };
  }
  /// <summary>
  /// Get the parent process of the current process.
  /// </summary>

  internal static Process? GetParentProcess(int id)
  {
    try
    {
      using var process = Process.GetProcessById(id);
      var parentId = GetParentProcessId(process);
      return parentId != 0 ? Process.GetProcessById(parentId) : null;
    }
    catch
    {
      return null;
    }
  }
}
