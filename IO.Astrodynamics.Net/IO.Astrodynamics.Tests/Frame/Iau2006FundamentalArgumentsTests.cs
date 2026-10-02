using IO.Astrodynamics.Frames;
using Xunit;

namespace IO.Astrodynamics.Tests.Frame;

public class Iau2006FundamentalArgumentsTests
{
    // SOFA reference values at t = 0 (J2000.0 TT)
    [Fact]
    public void MoonMeanAnomalyAtJ2000()
    {
        double l = Iau2006FundamentalArguments.MoonMeanAnomaly(0.0);
        Assert.Equal(2.35555574349387919, l, 1e-12);
    }

    [Fact]
    public void SunMeanAnomalyAtJ2000()
    {
        double lp = Iau2006FundamentalArguments.SunMeanAnomaly(0.0);
        Assert.Equal(6.24006012691328404, lp, 1e-12);
    }

    [Fact]
    public void MoonMeanArgumentOfLatitudeAtJ2000()
    {
        double f = Iau2006FundamentalArguments.MoonMeanArgumentOfLatitude(0.0);
        Assert.Equal(1.62790508153751912, f, 1e-12);
    }

    [Fact]
    public void MeanElongationAtJ2000()
    {
        double d = Iau2006FundamentalArguments.MeanElongation(0.0);
        Assert.Equal(5.19846658866019862, d, 1e-12);
    }

    [Fact]
    public void MoonAscendingNodeLongitudeAtJ2000()
    {
        double om = Iau2006FundamentalArguments.MoonAscendingNodeLongitude(0.0);
        Assert.Equal(2.18243919661567087, om, 1e-12);
    }

    // SOFA reference values at t = 0.06 (SOFA test date MJD 53736)
    [Fact]
    public void MoonMeanAnomalyAtSofaTestDate()
    {
        double l = Iau2006FundamentalArguments.MoonMeanAnomaly(0.06);
        Assert.Equal(5.70540257590276578, l, 1e-10);
    }

    [Fact]
    public void SunMeanAnomalyAtSofaTestDate()
    {
        double lp = Iau2006FundamentalArguments.SunMeanAnomaly(0.06);
        Assert.Equal(6.23906558446455595, lp, 1e-10);
    }

    [Fact]
    public void MoonMeanArgumentOfLatitudeAtSofaTestDate()
    {
        double f = Iau2006FundamentalArguments.MoonMeanArgumentOfLatitude(0.06);
        Assert.Equal(4.98104969960149813, f, 1e-10);
    }

    [Fact]
    public void MeanElongationAtSofaTestDate()
    {
        double d = Iau2006FundamentalArguments.MeanElongation(0.06);
        Assert.Equal(2.42197174633053791e-01, d, 1e-10);
    }

    [Fact]
    public void MoonAscendingNodeAtSofaTestDate()
    {
        double om = Iau2006FundamentalArguments.MoonAscendingNodeLongitude(0.06);
        Assert.Equal(1.57016569820372986e-01, om, 1e-10);
    }

    // Planetary arguments at SOFA test date
    [Fact]
    public void LambdaVenusAtSofaTestDate()
    {
        double lve = Iau2006FundamentalArguments.LambdaVenus(0.06);
        Assert.Equal(1.62400690247012847, lve, 1e-10);
    }

    [Fact]
    public void LambdaEarthAtSofaTestDate()
    {
        double lea = Iau2006FundamentalArguments.LambdaEarth(0.06);
        Assert.Equal(1.75281357086847578, lea, 1e-10);
    }

    [Fact]
    public void GeneralPrecessionAtSofaTestDate()
    {
        double lge = Iau2006FundamentalArguments.GeneralPrecession(0.06);
        Assert.Equal(1.46292439287599996e-03, lge, 1e-12);
    }
}
