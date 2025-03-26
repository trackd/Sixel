using Sixel.Terminal.Models;
using System.Collections;

namespace Sixel.Terminal;
public static class TerminalChecker
{
    /// <summary>
    /// Check the terminal for compatibility.
    /// use enviroment variables to try and figure out which terminal is being used.
    /// </summary>
    internal static TerminalInfo CheckTerminal()
    {
        var env = Environment.GetEnvironmentVariables();
        TerminalInfo? envTerminalInfo = null;

        // First check environment variables to identify the terminal
        foreach (DictionaryEntry item in env)
        {
            var key = item.Key?.ToString();
            var value = item.Value?.ToString();

            if (key == "TERM_PROGRAM" && value != null && Helpers.GetTerminal(value) is Terminals terminal)
            {
                if (Helpers.SupportedProtocol.TryGetValue(terminal, out var protocol))
                {
                    envTerminalInfo = new TerminalInfo
                    {
                        Terminal = terminal,
                        Protocol = protocol
                    };
                    // Console.WriteLine($"Detected terminal: {terminal} ({string.Join(", ", protocol)}) from TERM_PROGRAM={value}");
                    break;
                }
            }

            if (key != null && Helpers.GetTerminal(key) is Terminals _terminal)
            {
                if (Helpers.SupportedProtocol.TryGetValue(_terminal, out var protocol))
                {
                    envTerminalInfo = new TerminalInfo
                    {
                        Terminal = _terminal,
                        Protocol = protocol
                    };
                    // Console.WriteLine($"Detected terminal: {_terminal} ({string.Join(", ", protocol)}) from {key}={value}");
                    break;
                }
            }
        }

        // Then check VT capabilities and override protocol if supported
        if (Compatibility.TerminalSupportsKitty())
        {
            // Console.WriteLine($"Detected terminal: Kitty Graphics Protocol from VT capabilities.");
            return new TerminalInfo {
                Terminal = envTerminalInfo?.Terminal ?? Terminals.Kitty,
                Protocol = envTerminalInfo?.Protocol != null
                    ? (envTerminalInfo.Protocol.Contains(ImageProtocol.KittyGraphicsProtocol)
                        ? envTerminalInfo.Protocol
                        : [ImageProtocol.KittyGraphicsProtocol, .. envTerminalInfo.Protocol])
                    : [ImageProtocol.KittyGraphicsProtocol]
                        };
        }

        if (Compatibility.TerminalSupportsSixel())
        {
            // Console.WriteLine($"Detected terminal: Sixel from VT capabilities.");
            return new TerminalInfo {
                Terminal = envTerminalInfo?.Terminal ?? Terminals.MicrosoftTerminal,
                Protocol = envTerminalInfo?.Protocol != null
                    ? (envTerminalInfo.Protocol.Contains(ImageProtocol.Sixel)
                        ? envTerminalInfo.Protocol
                        : [ImageProtocol.Sixel, .. envTerminalInfo.Protocol])
                    : [ImageProtocol.Sixel]
            };
        }
        // Console.WriteLine($"No VT response, using from enviroment variable: {envTerminalInfo?.Terminal}");
        // Return environment-detected terminal or fallback to unknown
        return envTerminalInfo ?? new TerminalInfo
        {
            Terminal = Terminals.unknown,
            Protocol = [ImageProtocol.Blocks]
        };
    }
}
