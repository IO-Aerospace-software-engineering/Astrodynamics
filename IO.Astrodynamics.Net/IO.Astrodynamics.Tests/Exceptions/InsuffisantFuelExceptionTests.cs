using System;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Exceptions;
using Xunit;

namespace IO.Astrodynamics.Tests.Exceptions;

public class InsuffisantFuelExceptionTests
{
    [Fact]
    public void Create()
    {
        InsufficientFuelException fuelException = new InsufficientFuelException();
        Assert.True(double.IsNaN(fuelException.RequiredMass));
        Assert.True(double.IsNaN(fuelException.AvailableMass));
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

    // R03 of #668: the exception derives from InvalidOperationException (ExceptionBase removed) and carries the masses
    [Fact]
    public void IsAnInvalidOperationExceptionWithTheMasses()
    {
        var fuelException = new InsufficientFuelException("No enough fuel", 150.0, 100.0);
        Assert.IsAssignableFrom<InvalidOperationException>(fuelException);
        Assert.Equal(150.0, fuelException.RequiredMass);
        Assert.Equal(100.0, fuelException.AvailableMass);
        Assert.Equal("No enough fuel", fuelException.Message);
    }

    // A burn of more fuel than the tank holds: the masses of the burn and of the tank, the tank left as it was
    [Fact]
    public void ABurnOfMoreFuelThanTheTankHolds_ThrowsWithTheMasses()
    {
        var tank = new FuelTank("ft", 1000.0, 100.0, "model", "sn");
        var error = Assert.Throws<InsufficientFuelException>(() => tank.Burn(150.0));
        Assert.Equal(150.0, error.RequiredMass);
        Assert.Equal(100.0, error.AvailableMass);
        Assert.Contains("ft", error.Message);
        Assert.Equal(100.0, tank.Quantity);
    }
}
