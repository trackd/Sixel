using System.Diagnostics;
using Sixel.Terminal.Models;

namespace Sixel.Terminal
{
    public static class TerminalChecker
    {
        public static (Terminals Terminal, ImageProtocol Protocol)? CheckTerminal()
        {
            var variables = Environment.GetEnvironmentVariables();
            foreach (string key in variables.Keys)
            {
                if (Enum.TryParse(key, out Terminals terminalKey) && Helpers.EnvVars.TryGetValue(terminalKey, out var envVar))
                {
                    var envValue = variables[envVar]?.ToString();
                    if (envVar == "WT_SESSION")
                    {
                        return Native.ProcessHelper.CheckParent(Process.GetCurrentProcess().Id);
                    }
                    if (Enum.TryParse(envValue, out Terminals terminal))
                    {
                        return GetTerminalProtocol(terminal);
                    }
                }
            }
            if (variables.Contains("SESSIONNAME"))
            {
                return Native.ProcessHelper.CheckParent(Process.GetCurrentProcess().Id);
            }
            return null;
        }
        internal static (Terminals Terminal, ImageProtocol Protocol)? GetTerminalProtocol(Terminals terminal)
        {
            if (Helpers.SupportedProtocol.TryGetValue(terminal, out var protocol))
            {
                return (terminal, protocol);
            }
            return null;
        }
    }
}
