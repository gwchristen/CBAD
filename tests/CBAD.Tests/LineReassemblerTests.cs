using CBAD.Parsing;

namespace CBAD.Tests;

public class LineReassemblerTests
{
    [Fact]
    public void SingleChunk_OneLine_YieldsOneLine()
    {
        var r = new LineReassembler();
        var lines = r.Feed("hello\n");
        Assert.Single(lines);
        Assert.Equal("hello", lines[0]);
    }

    [Fact]
    public void ChunkSplitMidLine_FirstFeedYieldsNothing_SecondYieldsLine()
    {
        var r = new LineReassembler();
        var first = r.Feed("hel");
        Assert.Empty(first);

        var second = r.Feed("lo\n");
        Assert.Single(second);
        Assert.Equal("hello", second[0]);
    }

    [Fact]
    public void TwoCompleteLines_YieldsBoth()
    {
        var r = new LineReassembler();
        var lines = r.Feed("line1\nline2\n");
        Assert.Equal(2, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
    }

    [Fact]
    public void CrLf_LineEndings_StrippedToCleanLines()
    {
        var r = new LineReassembler();
        var lines = r.Feed("hello\r\nworld\r\n");
        Assert.Equal(2, lines.Count);
        Assert.Equal("hello", lines[0]);
        Assert.Equal("world", lines[1]);
    }

    [Fact]
    public void MultipleFeeds_AccumulateCorrectly()
    {
        var r = new LineReassembler();
        r.Feed("par");
        var second = r.Feed("t1\npar");
        var third  = r.Feed("t2\n");

        // "part1" completes on the second Feed call; "part2" completes on the third.
        Assert.Single(second);
        Assert.Equal("part1", second[0]);

        Assert.Single(third);
        Assert.Equal("part2", third[0]);
    }

    [Fact]
    public void BurstInput_ManyLinesInOneChunk_AllYielded()
    {
        var r = new LineReassembler();
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= 20; i++)
            sb.Append($"burst{i}\n");

        var lines = r.Feed(sb.ToString());

        Assert.Equal(20, lines.Count);
        for (int i = 1; i <= 20; i++)
            Assert.Equal($"burst{i}", lines[i - 1]);
    }

    [Fact]
    public void PartialLineAtStart_HeldUntilNewlineArrives()
    {
        var r = new LineReassembler();
        var first = r.Feed("incomplete");
        Assert.Empty(first);

        var second = r.Feed(" line\n");
        Assert.Single(second);
        Assert.Equal("incomplete line", second[0]);
    }

    [Fact]
    public void OnlyNewline_YieldsEmptyLine()
    {
        var r = new LineReassembler();
        var lines = r.Feed("\n");
        Assert.Single(lines);
        Assert.Equal(string.Empty, lines[0]);
    }

    [Fact]
    public void TrailingPartialLine_NotYieldedUntilNewline()
    {
        var r = new LineReassembler();
        var lines = r.Feed("complete\ntrailing");
        Assert.Single(lines);
        Assert.Equal("complete", lines[0]);

        var more = r.Feed(" more\n");
        Assert.Single(more);
        Assert.Equal("trailing more", more[0]);
    }
}
