using System;

namespace IO.Astrodynamics.CLI.Cli;

/// <summary>
/// An argument the command cannot read: an unknown object, enumeration value, unit or frame, a malformed number, epoch or
/// step, a combination of options the command refuses. Exit code 2 (R29 of #668).
/// </summary>
internal sealed class CliInputException : Exception
{
    public CliInputException(string message) : base(message)
    {
    }

    public CliInputException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// The kernels of the command cannot be loaded: none given, a path that does not exist, or a file SPICE refuses. Exit
/// code 3 (R29 of #668).
/// </summary>
internal sealed class CliKernelException : Exception
{
    public CliKernelException(string message) : base(message)
    {
    }

    public CliKernelException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
