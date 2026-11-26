using IO.Astrodynamics;
using JetBrains.Annotations;
using Xunit;

namespace IO.Astrodynamics.Tests;

[TestSubject(typeof(Enumeration))]
public class EnumerationTest
{
    [Fact]
    public void GetValueFromDescription()
    {
        var res = Enumeration.GetValueFromDescription<RelationalOperator>("<");
        Assert.Equal(RelationalOperator.LessThan, res);
    }
}