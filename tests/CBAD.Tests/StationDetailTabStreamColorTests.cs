using System.Drawing;
using System.Reflection;
using CBAD.UI;

namespace CBAD.Tests;

public class StationDetailTabStreamColorTests
{
    [Theory]
    [InlineData("Parse failed: bad line", 255, 100, 100)]
    [InlineData("1,2", 255, 100, 100)]
    [InlineData("1,2,not-a-code", 255, 100, 100)]
    [InlineData("1,2,16,foo", 255, 80, 80)]
    [InlineData("1,2,11,foo", 100, 220, 255)]
    [InlineData("1,2,129,foo", 255, 200, 80)]
    [InlineData("1,2,250,foo", 130, 210, 130)]
    [InlineData("1,2,42,foo", 200, 200, 200)]
    public void GetLineColor_ReturnsExpectedColor(string line, int r, int g, int b)
    {
        var expected = Color.FromArgb(r, g, b);
        var actual = InvokeGetLineColor(line);

        Assert.Equal(expected, actual);
    }

    private static Color InvokeGetLineColor(string line)
    {
        var method = typeof(StationDetailTab).GetMethod("GetLineColor", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null, [line]);
        Assert.IsType<Color>(result);
        return (Color)result;
    }
}
