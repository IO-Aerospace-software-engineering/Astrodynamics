using System;
using IO.Astrodynamics.Exceptions;
using Xunit;

namespace IO.Astrodynamics.Tests.Exceptions;

public class InsuffisantFuelExceptionTests
{
    [Fact]
    public void Create()
    {
        InsufficientFuelException fuelException = new InsufficientFuelException();
    }
    
    [Fact]
    public void CreateWithMessage()
    {
        InsufficientFuelException fuelException = new InsufficientFuelException("No enough fuel");
        Assert.Equal("No enough fuel",fuelException.Message);
    }
    
    [Fact]
    public void CreateWithMessageAndInnerException()
    {
        InsufficientFuelException fuelException = new InsufficientFuelException("No enough fuel",new InvalidOperationException());
        Assert.Equal("No enough fuel",fuelException.Message);
        Assert.IsType<InvalidOperationException>(fuelException.InnerException);
    }
}