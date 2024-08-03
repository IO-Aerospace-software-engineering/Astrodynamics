using System;

namespace IO.Astrodynamics.Exceptions;

public class InsufficientFuelException : ExceptionBase
{
    public InsufficientFuelException()
    {
    }

    public InsufficientFuelException(string message) : base(message)
    {
    }

    public InsufficientFuelException(string message, Exception innerException) : base(message, innerException)
    {
    }
}