using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Time;

public class TimeFrameTests
{
    [Fact]
    public void Equality()
    {
        Assert.Equal(TimeFrame.UTCFrame, TimeFrame.UTCFrame);
        Assert.True(TimeFrame.UTCFrame != TimeFrame.TDBFrame);
        Assert.True(TimeFrame.UTCFrame == TimeFrame.UTCFrame);
        Assert.True(TimeFrame.UTCFrame.Equals(TimeFrame.UTCFrame));
        Assert.False(TimeFrame.UTCFrame.Equals(null));
    }
}