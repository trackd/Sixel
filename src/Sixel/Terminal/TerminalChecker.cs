using Sixel.Terminal.Models;
using System.Collections;

namespace Sixel.Terminal;
/// <summary>
/// Provides methods to detect and check terminal compatibility and supported image protocols based on environment variables.
/// </summary>
public static class TerminalChecker
{
    /// <summary>
    /// Check the terminal for compatibility.
    /// use enviroment variables to try and figure out which terminal is being used.
    /// this is just a pain, order is weird and edge cases.. it's a mess.
    /// </summary>
    internal static TerminalInfo CheckTerminal()
    {
        var env = Environment.GetEnvironmentVariables();
        Terminals detectedTerminal = Terminals.unknown;
        ImageProtocol[] detectedProtocols = [ImageProtocol.Blocks];

        // 1. Explicit checks for VSCode/WezTerm with version logic
        if (env["TERM_PROGRAM_VERSION"] is string termProgramVersion && env["TERM_PROGRAM"] is string termProgram)
        {
            var terminal = Helpers.GetTerminal(termProgram);
            if (terminal == Terminals.VSCode && termProgramVersion != null)
            {
                var dashIdx = termProgramVersion.IndexOf('-');
                var versionPart = dashIdx > 0 ? termProgramVersion.Substring(0, dashIdx) : termProgramVersion;
                if (Version.TryParse(versionPart, out var parsedVersion))
                {
                    var minVSCodeVersion = new Version(1, 102, 0);
                    if (parsedVersion >= minVSCodeVersion)
                    {
                        detectedTerminal = terminal;
                        detectedProtocols = [ImageProtocol.Sixel, ImageProtocol.InlineImageProtocol];
                    }
                }
            }
            else if (terminal == Terminals.WezTerm && termProgramVersion != null)
            {
                var parts = termProgramVersion.Split('-');
                if (parts.Length > 0 && DateTime.TryParseExact(parts[0], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var buildDate))
                {
                    var minWezTermDate = new DateTime(2025, 3, 20);
                    if (buildDate >= minWezTermDate)
                    {
                        detectedTerminal = terminal;
                        detectedProtocols = [ImageProtocol.KittyGraphicsProtocol, ImageProtocol.Sixel, ImageProtocol.InlineImageProtocol];
                    }
                }
            }
            // Fallback to supported protocol if not matched by version
            if (detectedTerminal == Terminals.unknown && Helpers.SupportedProtocol.TryGetValue(terminal, out var protocol))
            {
                detectedTerminal = terminal;
                detectedProtocols = protocol;
            }
        }

        // 2. Check for other well-known env variables (e.g., WT_SESSION for Windows Terminal)
        if (detectedTerminal == Terminals.unknown)
        {
            foreach (var known in Helpers.GetEnvironmentVariables())
            {
                if (env[known] != null)
                {
                    var terminal = Helpers.GetTerminal(known);
                    if (Helpers.SupportedProtocol.TryGetValue(terminal, out var protocol))
                    {
                        detectedTerminal = terminal;
                        detectedProtocols = protocol;
                        break;
                    }
                }
            }
        }

        // 3. Fallback: scan all env vars for known terminal signatures
        if (detectedTerminal == Terminals.unknown)
        {
            foreach (DictionaryEntry item in env)
            {
                var key = item.Key?.ToString();
                var value = item.Value?.ToString();
                if (key != null && Helpers.GetTerminal(key) is Terminals _terminal && _terminal != Terminals.unknown)
                {
                    if (Helpers.SupportedProtocol.TryGetValue(_terminal, out var protocol))
                    {
                        detectedTerminal = _terminal;
                        detectedProtocols = protocol;
                        break;
                    }
                }
                if (value != null && Helpers.GetTerminal(value) is Terminals _terminal2 && _terminal2 != Terminals.unknown)
                {
                    if (Helpers.SupportedProtocol.TryGetValue(_terminal2, out var protocol))
                    {
                        detectedTerminal = _terminal2;
                        detectedProtocols = protocol;
                        break;
                    }
                }
            }
        }

        // 4. VT/ANSI fallback: autodetect Kitty/Sixel support and augment protocol list
        var protocolList = new List<ImageProtocol>(detectedProtocols);
        bool kittySupported = Compatibility.TerminalSupportsKitty();
        bool sixelSupported = Compatibility.TerminalSupportsSixel();
        if (kittySupported && !protocolList.Contains(ImageProtocol.KittyGraphicsProtocol))
            protocolList.Insert(0, ImageProtocol.KittyGraphicsProtocol);
        if (sixelSupported && !protocolList.Contains(ImageProtocol.Sixel))
            protocolList.Insert(0, ImageProtocol.Sixel);

        return new TerminalInfo {
            Terminal = detectedTerminal,
            Protocol = [.. protocolList]
        };
    }
}
