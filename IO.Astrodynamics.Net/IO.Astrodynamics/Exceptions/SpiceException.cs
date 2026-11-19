// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.Exceptions;

/// <summary>
/// The error SPICE signals in a call of the library (R03 of #668): a kernel it cannot load, a body, a frame or an epoch the
/// loaded kernels do not cover. <see cref="ShortMessage"/> is SPICE's short message, such as
/// <c>SPICE(SPKINSUFFDATA)</c> or <c>SPICE(KERNELVARNOTFOUND)</c>, the code to test; <see cref="LongMessage"/> its long one;
/// <see cref="Exception.Message"/> the call of the library that failed, followed by SPICE's message.
/// </summary>
/// <remarks>
/// It derives from <see cref="InvalidOperationException"/>, the type the library threw for these errors before 11.0.0, so
/// that a <c>catch (InvalidOperationException)</c> still catches it. A failure of the native layer that is not SPICE's (a C++
/// exception of the native layer with no SPICE error pending) stays an <see cref="InvalidOperationException"/>, and a frame
/// name the loaded kernels do not define an <see cref="ArgumentException"/> whose inner exception is this one.
/// </remarks>
public class SpiceException : InvalidOperationException
{
    /// <summary>SPICE's short message, such as <c>SPICE(SPKINSUFFDATA)</c>; empty when none was given.</summary>
    public string ShortMessage { get; } = string.Empty;

    /// <summary>
    /// SPICE's long message, without the context the library adds; empty when SPICE gave none (the errors of
    /// <c>prop2b_c</c>, which set only the short one).
    /// </summary>
    public string LongMessage { get; } = string.Empty;

    /// <summary>A SPICE error with no message.</summary>
    public SpiceException()
    {
    }

    /// <summary>A SPICE error with <paramref name="message"/> and no short or long message.</summary>
    /// <param name="message">The message.</param>
    public SpiceException(string message) : base(message)
    {
    }

    /// <summary>A SPICE error with <paramref name="message"/>, caused by <paramref name="innerException"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public SpiceException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>A SPICE error with SPICE's short and long messages.</summary>
    /// <param name="message">The call that failed, followed by SPICE's message.</param>
    /// <param name="shortMessage">SPICE's short message, such as <c>SPICE(SPKINSUFFDATA)</c>.</param>
    /// <param name="longMessage">SPICE's long message, empty when it gave none.</param>
    public SpiceException(string message, string shortMessage, string longMessage) : base(message)
    {
        ShortMessage = shortMessage ?? string.Empty;
        LongMessage = longMessage ?? string.Empty;
    }

    /// <summary>A SPICE error with SPICE's short and long messages, caused by <paramref name="innerException"/>.</summary>
    /// <param name="message">The call that failed, followed by SPICE's message.</param>
    /// <param name="shortMessage">SPICE's short message, such as <c>SPICE(SPKINSUFFDATA)</c>.</param>
    /// <param name="longMessage">SPICE's long message, empty when it gave none.</param>
    /// <param name="innerException">The cause.</param>
    public SpiceException(string message, string shortMessage, string longMessage, Exception innerException)
        : base(message, innerException)
    {
        ShortMessage = shortMessage ?? string.Empty;
        LongMessage = longMessage ?? string.Empty;
    }
}
