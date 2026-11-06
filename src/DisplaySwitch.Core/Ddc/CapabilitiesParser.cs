using System.Globalization;

namespace DisplaySwitch.Core.Ddc;

/// <summary>
/// Parses MCCS capabilities strings such as
/// <c>(prot(monitor)type(lcd)model(FALCON)vcp(02 10 60(01 03))mccs_ver(2.0))</c>.
/// </summary>
/// <remarks>
/// Monitor firmware is inconsistent: some omit spaces between codes, some leave the outer parentheses
/// unbalanced, and some include vendor junk. The parser is deliberately lenient. It extracts what it
/// can recognize and ignores the rest, because a partially useful result is better than none.
/// </remarks>
public static class CapabilitiesParser
{
    public static MonitorCapabilities Parse(string? capabilities)
    {
        if (string.IsNullOrWhiteSpace(capabilities))
        {
            return MonitorCapabilities.Empty;
        }

        var entries = ReadTopLevelEntries(Unwrap(capabilities.Trim()));
        entries.TryGetValue("model", out var model);
        entries.TryGetValue("mccs_ver", out var mccsVersion);
        var vcp = entries.TryGetValue("vcp", out var vcpBody) ? ParseVcpList(vcpBody) : new Dictionary<byte, IReadOnlyList<byte>>();

        return new MonitorCapabilities(NullIfBlank(model), NullIfBlank(mccsVersion), vcp);
    }

    private static string Unwrap(string text) =>
        text.StartsWith('(') && FindClosing(text, 0) == text.Length - 1 ? text[1..^1] : text.TrimStart('(');

    private static Dictionary<string, string> ReadTopLevelEntries(string text)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf('(', position);
            if (open < 0)
            {
                break;
            }

            var key = text[position..open].Trim();
            var close = FindClosing(text, open);
            var end = close < 0 ? text.Length : close;
            if (key.Length > 0)
            {
                entries.TryAdd(key, text[(open + 1)..end]);
            }

            position = end + 1;
        }

        return entries;
    }

    private static Dictionary<byte, IReadOnlyList<byte>> ParseVcpList(string body)
    {
        var features = new Dictionary<byte, IReadOnlyList<byte>>();
        var position = 0;
        while (position < body.Length)
        {
            if (!TryReadHexByte(body, ref position, out var code))
            {
                position++;
                continue;
            }

            var values = new List<byte>();
            SkipWhitespace(body, ref position);
            if (position < body.Length && body[position] == '(')
            {
                var close = FindClosing(body, position);
                var end = close < 0 ? body.Length : close;
                var valuePosition = position + 1;
                while (valuePosition < end)
                {
                    if (TryReadHexByte(body, ref valuePosition, out var value))
                    {
                        values.Add(value);
                    }
                    else
                    {
                        valuePosition++;
                    }
                }

                position = end + 1;
            }

            features.TryAdd(code, values);
        }

        return features;
    }

    /// <summary>Reads exactly two hex digits, because some firmware writes codes without separators.</summary>
    private static bool TryReadHexByte(string text, ref int position, out byte value)
    {
        SkipWhitespace(text, ref position);
        value = 0;
        if (position + 1 >= text.Length
            || !char.IsAsciiHexDigit(text[position])
            || !char.IsAsciiHexDigit(text[position + 1]))
        {
            return false;
        }

        value = byte.Parse(text.AsSpan(position, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        position += 2;
        return true;
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }

    /// <summary>Returns the index of the parenthesis closing the one at <paramref name="open"/>, or -1.</summary>
    private static int FindClosing(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            depth += text[i] switch { '(' => 1, ')' => -1, _ => 0 };
            if (depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
