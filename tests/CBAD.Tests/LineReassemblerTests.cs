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
        r.Feed("t1\npar");
        var lines = r.Feed("t2\n");
        Assert.Equal(2, lines.Count);
        Assert.Equal("part1", lines[0]);
        Assert.Equal("part2", lines[1]);
    }
}
