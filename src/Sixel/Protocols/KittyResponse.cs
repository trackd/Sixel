using System.Globalization;

namespace Sixel.Protocols;

// The net472 uint parser has no span overload.
#pragma warning disable CA1846

/// <summary>A graphics acknowledgement. Parsing never reads console input.</summary>
public sealed class KittyResponse {
    public uint ImageId { get; private set; }
    public uint ImageNumber { get; private set; }
    public uint PlacementId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public bool Success => Message == "OK";
    public string? ErrorCode => Success ? null : Message.Split(':')[0];

    /// <summary>Parses one complete ESC _ G ... ESC backslash response.</summary>
    public static bool TryParse(string? packet, out KittyResponse? response) {
        response = null;
        if (packet is null || !packet.StartsWith("\u001b_G", StringComparison.Ordinal) ||
            !packet.EndsWith("\u001b\\", StringComparison.Ordinal)) return false;
        int separator = packet.IndexOf(';');
        if (separator < 3 || separator >= packet.Length - 3) return false;
        string message = packet.Substring(separator + 1, packet.Length - separator - 3);
        if (message.Any(character => character < ' ' || character > '~')) return false;
        var parsed = new KittyResponse { Message = message };
        var seen = new HashSet<char>();
        foreach (string entry in packet.Substring(3, separator - 3).Split(',')) {
            if (entry.Length < 3 || entry[1] != '=' || !seen.Add(entry[0]) ||
                !uint.TryParse(entry.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out uint value)) return false;
            switch (entry[0]) {
                case 'i': parsed.ImageId = value; break;
                case 'I': parsed.ImageNumber = value; break;
                case 'p': parsed.PlacementId = value; break;
                default: return false;
            }
        }
        if (!seen.Contains('i') && !seen.Contains('I')) return false;
        response = parsed;
        return true;
    }
}
