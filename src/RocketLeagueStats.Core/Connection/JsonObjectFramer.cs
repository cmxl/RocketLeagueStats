namespace RocketLeagueStats.Core.Connection;

/// <summary>
/// Allocation-free, byte-level scanner for top-level JSON object boundaries.
/// Tracks brace depth while honouring string literals (with backslash escapes) so
/// braces inside strings — including the Stats API's escaped-JSON Data payloads —
/// do not disturb the count.
/// </summary>
internal static class JsonObjectFramer
{
    private const byte Quote = (byte)'"';
    private const byte Backslash = (byte)'\\';
    private const byte OpenBrace = (byte)'{';
    private const byte CloseBrace = (byte)'}';

    /// <summary>
    /// Attempts to identify a complete top-level JSON object at the start of <paramref name="input"/>,
    /// after skipping any leading insignificant whitespace.
    /// </summary>
    /// <param name="input">Bytes to scan. Caller owns the lifetime.</param>
    /// <param name="consumed">
    /// On success, the number of bytes from the start of <paramref name="input"/> that have been
    /// consumed (leading whitespace + the object itself). Caller advances its read cursor by this
    /// amount.
    /// </param>
    /// <param name="objectStart">On success, the start index of the object inside <paramref name="input"/> (skipping leading whitespace).</param>
    /// <param name="objectLength">On success, the byte length of the object including its outer braces.</param>
    /// <returns>True when a complete top-level object is available.</returns>
    public static bool TryFind(ReadOnlySpan<byte> input, out int consumed, out int objectStart, out int objectLength)
    {
        var i = 0;
        while (i < input.Length && IsWhitespace(input[i]))
        {
            i++;
        }

        if (i >= input.Length || input[i] != OpenBrace)
        {
            consumed = 0;
            objectStart = 0;
            objectLength = 0;
            return false;
        }

        var start = i;
        var depth = 0;
        var inString = false;
        var escapeNext = false;

        while (i < input.Length)
        {
            var b = input[i];

            if (escapeNext)
            {
                escapeNext = false;
                i++;
                continue;
            }

            if (inString)
            {
                if (b == Backslash)
                {
                    escapeNext = true;
                }
                else if (b == Quote)
                {
                    inString = false;
                }

                i++;
                continue;
            }

            if (b == Quote)
            {
                inString = true;
            }
            else if (b == OpenBrace)
            {
                depth++;
            }
            else if (b == CloseBrace)
            {
                depth--;
                if (depth == 0)
                {
                    objectStart = start;
                    objectLength = i - start + 1;
                    consumed = i + 1;
                    return true;
                }
            }

            i++;
        }

        consumed = 0;
        objectStart = 0;
        objectLength = 0;
        return false;
    }

    private static bool IsWhitespace(byte b) =>
        b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
