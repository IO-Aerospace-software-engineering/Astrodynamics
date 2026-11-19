// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.CCSDS;

/// <summary>
/// The common base of the exceptions of the CCSDS readers (R03 of #668): a document an OMM, OPM or CDM reader cannot read,
/// whether it fails at parsing (<see cref="OMM.OmmParseException"/>, <see cref="OPM.OpmParseException"/>,
/// <see cref="CDM.CdmParseException"/>) or at validation (<see cref="OMM.OmmValidationException"/>,
/// <see cref="OPM.OpmValidationException"/>), so that one <c>catch (CcsdsFormatException)</c> covers them all.
/// </summary>
/// <remarks>
/// It derives from <see cref="FormatException"/>, the type of an input whose format is wrong. Before 11.0.0 the five types
/// derived from <see cref="Exception"/> alone; their names, members and messages are unchanged.
/// </remarks>
public class CcsdsFormatException : FormatException
{
    /// <summary>A CCSDS document that cannot be read, with no message.</summary>
    public CcsdsFormatException()
    {
    }

    /// <summary>A CCSDS document that cannot be read, with <paramref name="message"/>.</summary>
    /// <param name="message">The message.</param>
    public CcsdsFormatException(string message) : base(message)
    {
    }

    /// <summary>A CCSDS document that cannot be read, with <paramref name="message"/>, caused by
    /// <paramref name="innerException"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public CcsdsFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
