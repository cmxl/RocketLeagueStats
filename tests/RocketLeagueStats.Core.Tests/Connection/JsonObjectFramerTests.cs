using System.Text;
using RocketLeagueStats.Core.Connection;

namespace RocketLeagueStats.Core.Tests.Connection;

public class JsonObjectFramerTests
{
    [Fact]
    public void Finds_a_simple_top_level_object()
    {
        var input = "{\"a\":1}"u8;

        var ok = JsonObjectFramer.TryFind(input, out var consumed, out var start, out var length);

        Assert.True(ok);
        Assert.Equal(0, start);
        Assert.Equal(7, length);
        Assert.Equal(7, consumed);
    }

    [Fact]
    public void Skips_leading_whitespace_before_the_object()
    {
        var input = "  \r\n\t{\"a\":1}"u8;

        var ok = JsonObjectFramer.TryFind(input, out var consumed, out var start, out var length);

        Assert.True(ok);
        Assert.Equal(5, start);
        Assert.Equal(7, length);
        Assert.Equal(12, consumed);
    }

    [Fact]
    public void Returns_false_for_a_partial_object_so_caller_can_buffer_more()
    {
        var input = "{\"a\":1"u8;   // missing closing brace

        var ok = JsonObjectFramer.TryFind(input, out var consumed, out _, out _);

        Assert.False(ok);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Treats_braces_inside_string_literals_as_payload_not_structure()
    {
        // Every '{' and '}' here lives inside the value of "Data". Depth must stay at 1 until the outer '}'.
        var input = """{"Data":"{\"a\":1,\"nested\":{\"b\":2}}"}"""u8;

        var ok = JsonObjectFramer.TryFind(input, out var consumed, out _, out var length);

        Assert.True(ok);
        Assert.Equal(input.Length, length);
        Assert.Equal(input.Length, consumed);
    }

    [Fact]
    public void Honours_backslash_escapes_inside_strings()
    {
        // The string contains a literal backslash followed by a quote (\"), which the escape state machine
        // must consume as a single escaped char — otherwise the closing quote count is off by one.
        var input = """{"k":"a\"b\\c"}"""u8;

        var ok = JsonObjectFramer.TryFind(input, out var consumed, out _, out var length);

        Assert.True(ok);
        Assert.Equal(input.Length, length);
        Assert.Equal(input.Length, consumed);
    }

    [Fact]
    public void Walks_past_garbage_before_the_first_open_brace_only_when_it_is_whitespace()
    {
        // Non-whitespace garbage should NOT be silently swallowed — caller must see "no object" so it can
        // decide whether to drop or recover. The first byte 'X' is non-whitespace and not '{' — return false.
        var input = "X{\"a\":1}"u8;

        var ok = JsonObjectFramer.TryFind(input, out var consumed, out _, out _);

        Assert.False(ok);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Caller_can_loop_to_extract_back_to_back_objects_in_one_buffer()
    {
        // Mimics what RunFramedLoopAsync does after one TCP read returns multiple coalesced messages.
        var input = """{"Event":"A"}{"Event":"B"} {"Event":"C"}"""u8.ToArray();

        var found = new List<string>();
        var cursor = 0;
        while (JsonObjectFramer.TryFind(input.AsSpan(cursor), out var consumed, out var start, out var length))
        {
            found.Add(Encoding.UTF8.GetString(input, cursor + start, length));
            cursor += consumed;
        }

        Assert.Equal(["""{"Event":"A"}""", """{"Event":"B"}""", """{"Event":"C"}"""], found);
    }
}
