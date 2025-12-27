using System.Collections;
using Sixel.Terminal.Models;

namespace Sixel.Terminal;
/// <summary>
/// Provides methods to detect and check terminal compatibility and supported image protocols based on environment variables.
/// </summary>
public static class TerminalChecker {
    /// <summary>
    /// Check the terminal for compatibility.
    /// use enviroment variables to try and figure out which terminal is being used.
    /// this is just a pain, order is weird and edge cases.. it's a mess.
    /// </summary>
    internal static TerminalInfo CheckTerminal() {
        IDictionary env = Environment.GetEnvironmentVariables();
        Terminals detectedTerminal = Terminals.unknown;
        ImageProtocol[] detectedProtocols = [ImageProtocol.Blocks];

        // 1. Explicit checks for VSCode/WezTerm with version logic
        if (env["TERM_PROGRAM_VERSION"] is string termProgramVersion && env["TERM_PROGRAM"] is string termProgram) {
            Terminals terminal = Helpers.GetTerminal(termProgram);
            if (terminal == Terminals.VSCode && termProgramVersion != null) {
                int dashIdx = termProgramVersion.IndexOf('-');
#if NET8_0_OR_GREATER
                string versionPart = dashIdx > 0 ? termProgramVersion[..dashIdx] : termProgramVersion;
#else
// net472 cant do range syntax..
                string versionPart = dashIdx > 0 ? termProgramVersion.Substring(0, dashIdx) : termProgramVersion;
#endif
                if (Version.TryParse(versionPart, out Version? parsedVersion)) {
                    var minVSCodeVersion = new Version(1, 102, 0);
                    if (parsedVersion >= minVSCodeVersion) {
                        detectedTerminal = terminal;
                        detectedProtocols = [ImageProtocol.Sixel, ImageProtocol.InlineImageProtocol];
                    }
                }
            }
            else if (terminal == Terminals.WezTerm && termProgramVersion != null) {
                string[] parts = termProgramVersion.Split('-');
                if (parts.Length > 0 && DateTime.TryParseExact(parts[0], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime buildDate)) {
                    var minWezTermDate = new DateTime(2025, 3, 20);
                    if (buildDate >= minWezTermDate) {
                        detectedTerminal = terminal;
                        detectedProtocols = [ImageProtocol.KittyGraphicsProtocol, ImageProtocol.Sixel, ImageProtocol.InlineImageProtocol];
                    }
                }
            }
            // Fallback to supported protocol if not matched by version
            if (detectedTerminal == Terminals.unknown && Helpers.SupportedProtocol.TryGetValue(terminal, out ImageProtocol[]? protocol)) {
                detectedTerminal = terminal;
                detectedProtocols = protocol;
            }
        }

        // 2. Check for other well-known env variables (e.g., WT_SESSION for Windows Terminal)
        if (detectedTerminal == Terminals.unknown) {
            foreach (string known in Helpers.GetEnvironmentVariables()) {
                if (env[known] != null) {
                    Terminals terminal = Helpers.GetTerminal(known);
                    if (Helpers.SupportedProtocol.TryGetValue(terminal, out ImageProtocol[]? protocol)) {
                        detectedTerminal = terminal;
                        detectedProtocols = protocol;
                        break;
                    }
                }
            }
        }

        // 3. Fallback: scan all env vars for known terminal signatures
        if (detectedTerminal == Terminals.unknown) {
            foreach (DictionaryEntry item in env) {
                string? key = item.Key?.ToString();
                string? value = item.Value?.ToString();
                if (key != null && Helpers.GetTerminal(key) is Terminals _terminal && _terminal != Terminals.unknown) {
                    if (Helpers.SupportedProtocol.TryGetValue(_terminal, out ImageProtocol[]? protocol)) {
                        detectedTerminal = _terminal;
                        detectedProtocols = protocol;
                        break;
                    }
                }
                if (value != null && Helpers.GetTerminal(value) is Terminals _terminal2 && _terminal2 != Terminals.unknown) {
                    if (Helpers.SupportedProtocol.TryGetValue(_terminal2, out ImageProtocol[]? protocol)) {
                        detectedTerminal = _terminal2;
                        detectedProtocols = protocol;
                        break;
                    }
                }
            }
        }

        // 4. VT/ANSI fallback: autodetect Kitty/Sixel support and augment protocol list
        List<ImageProtocol>? protocolList = [.. detectedProtocols];
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
