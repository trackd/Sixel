using System.Globalization;
using System.IO.Compression;
using System.Text;

// These APIs must also compile against .NET Framework 4.7.2.
#pragma warning disable CA1510, CA1864, CA2249

namespace Sixel.Protocols;

/// <summary>Kitty graphics wire encoder. Does not access the terminal or consume responses.</summary>
public static class KittyProtocol {
    private const string Escape = "\u001b";

    /// <summary>
    /// Encodes one complete command. Controls are case-sensitive key=value pairs.
    /// Payload is image bytes for direct transfers, or UTF-8 file/shared-memory names otherwise.
    /// Compression, when requested with o=z, is applied to direct payloads here.
    /// </summary>
    public static string Encode(IEnumerable<string> control, byte[]? payload = null, bool tmux = false, int chunkSize = 4096) {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteTo(writer, control, payload, tmux, chunkSize);
        return writer.ToString();
    }

    /// <summary>Writes complete packets without building a second full base64/output string. Does not close the writer.</summary>
    public static void WriteTo(TextWriter writer, IEnumerable<string> control, byte[]? payload = null, bool tmux = false, int chunkSize = 4096) {
        if (writer is null) throw new ArgumentNullException(nameof(writer));
        if (chunkSize < 4 || chunkSize > 4096 || chunkSize % 4 != 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be a multiple of four between 4 and 4096.");
        Dictionary<char, string> values = Parse(control);
        char action = values.TryGetValue('a', out string? a) ? a[0] : 't';
        bool transmission = "tTqf".IndexOf(action) >= 0;
        bool direct = !values.TryGetValue('t', out string? medium) || medium == "d";
        string allowed = action switch {
            't' or 'q' => "aqftsvSOiIopN",
            'T' => "aqftsvSOiIopNxywhXYcrCUzPQHV",
            'f' => "aqftsvSOiIoNxycrzXY",
            'p' => "aqiIpxywhXYcrCUzPQHV",
            'd' => "aqdiIpxyzr",
            'a' => "aqiIsrzcv",
            'c' => "aqiIcrxywhXYC",
            _ => string.Empty
        };
        if (values.Keys.Any(key => allowed.IndexOf(key) < 0))
            throw new ArgumentException("A control is not valid for this action.", nameof(control));
        if (transmission != (payload is { Length: > 0 }))
            throw new ArgumentException("Transmission commands require a payload; control commands cannot carry one.", nameof(payload));
        if (values.ContainsKey('i') && values.ContainsKey('I'))
            throw new ArgumentException("Specify either i (image ID) or I (image number), not both.", nameof(control));
        if ("pfacq".IndexOf(action) >= 0 && !HasNonzero(values, 'i') && !HasNonzero(values, 'I'))
            throw new ArgumentException("This action requires a nonzero image ID or image number.", nameof(control));
        if (HasNonzero(values, 'U') && HasNonzero(values, 'P'))
            throw new ArgumentException("Virtual placements cannot be relative placements.", nameof(control));
        if (transmission && (!values.TryGetValue('f', out string? format) || format != "100") &&
            (!HasNonzero(values, 's') || !HasNonzero(values, 'v')))
            throw new ArgumentException("Raw RGB/RGBA transfers require positive s and v pixel dimensions.", nameof(control));
        if (action == 'a' && values.TryGetValue('s', out string? state) && state is not ("0" or "1" or "2" or "3"))
            throw new ArgumentException("Animation state s must be 0, 1, 2, or 3.", nameof(control));
        if (action == 'f' && values.TryGetValue('X', out string? blend) && blend is not ("0" or "1"))
            throw new ArgumentException("Frame blending X must be 0 or 1.", nameof(control));
        if (transmission && direct && (!values.TryGetValue('f', out string? pixelFormat) || pixelFormat != "100")) {
            ulong expected = (ulong)uint.Parse(values['s'], CultureInfo.InvariantCulture) * uint.Parse(values['v'], CultureInfo.InvariantCulture);
            int channels = pixelFormat == "24" ? 3 : 4;
            if (expected > (ulong)(int.MaxValue / channels) || payload!.Length != (long)expected * channels)
                throw new ArgumentException("Raw payload length does not match s, v, and f.", nameof(payload));
        }
        if (transmission && !direct && values.TryGetValue('f', out string? referenceFormat) && referenceFormat == "100" &&
            values.ContainsKey('o') && !HasNonzero(values, 'S'))
            throw new ArgumentException("Compressed PNG references require S, the uncompressed PNG byte count.", nameof(control));
        if (!values.ContainsKey('q'))
            values.Add('q', "2");

        byte[] data = payload ?? [];
        if (transmission && direct && values.TryGetValue('o', out string? compression) && compression == "z") {
            if (values.TryGetValue('f', out string? compressedFormat) && compressedFormat == "100")
                values['S'] = data.Length.ToString(CultureInfo.InvariantCulture);
            data = Compress(data);
        }
        if (!direct && data.Length > 3072)
            throw new ArgumentException("Encoded transfer names cannot exceed 4096 bytes.", nameof(payload));
        string header = string.Join(",", values.Select(pair => pair.Key + "=" + pair.Value));
        int position = 0;
        do {
            int count = Math.Min(direct ? chunkSize / 4 * 3 : data.Length, data.Length - position);
            bool more = position + count < data.Length;
            string chunkHeader = position == 0 ? header :
                (action == 'f' ? "a=f," : string.Empty) + "q=" + values['q'];
            if (transmission && direct)
                chunkHeader += more ? ",m=1" : ",m=0";
            var packetBuilder = new StringBuilder(Escape + "_G" + chunkHeader);
            if (transmission) packetBuilder.Append(';').Append(Convert.ToBase64String(data, position, count));
            string packet = packetBuilder.Append('\u001b').Append('\\').ToString();
            writer.Write(tmux ? Escape + "Ptmux;" + packet.Replace(Escape, Escape + Escape) + Escape + "\\" : packet);
            position += count;
        } while (position < data.Length);
    }

    internal static Dictionary<char, string> Parse(IEnumerable<string> control) {
        if (control is null)
            throw new ArgumentNullException(nameof(control));
        var values = new Dictionary<char, string>();
        foreach (string entry in control) {
            if (entry is null || entry.Length < 3 || entry[1] != '=')
                throw new ArgumentException("Controls must be individual case-sensitive key=value entries.", nameof(control));
            char key = entry[0];
            string value = entry.Substring(2);
            bool valid = key switch {
                'a' => value.Length == 1 && "tTqpdfac".Contains(value),
                't' => value.Length == 1 && "dfts".Contains(value),
                'o' => value == "z",
                'd' => value.Length == 1 && "aAcCnNiIpPqQrRxXyYzZfF".Contains(value),
                'f' => value is "24" or "32" or "100",
                'q' => value is "0" or "1" or "2",
                'C' or 'U' => value is "0" or "1",
                'z' or 'H' or 'V' => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
                _ => "svSOiIpxywhXYcrPQN".IndexOf(key) >= 0 &&
                    uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            };
            if (!valid || values.ContainsKey(key))
                throw new ArgumentException("Invalid, duplicate, or encoder-owned control: " + entry, nameof(control));
            values.Add(key, value);
        }
        return values;
    }

    private static bool HasNonzero(Dictionary<char, string> values, char key) =>
        values.TryGetValue(key, out string? value) && uint.TryParse(value, out uint number) && number != 0;

    internal static byte[] Compress(byte[] data) {
        using var output = new MemoryStream();
#if NET472
        // RFC 1950 envelope around the RFC 1951 stream emitted by DeflateStream.
        output.WriteByte(0x78);
        output.WriteByte(0x9c);
        using (var compressor = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            compressor.Write(data, 0, data.Length);
        uint first = 1, second = 0;
        foreach (byte value in data) {
            first = (first + value) % 65521;
            second = (second + first) % 65521;
        }
        uint checksum = (second << 16) | first;
        for (int shift = 24; shift >= 0; shift -= 8)
            output.WriteByte((byte)(checksum >> shift));
#else
        using (var compressor = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            compressor.Write(data, 0, data.Length);
#endif
        return output.ToArray();
    }
}
