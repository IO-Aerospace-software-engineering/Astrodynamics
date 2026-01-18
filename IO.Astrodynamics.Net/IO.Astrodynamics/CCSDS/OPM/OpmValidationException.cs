// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Text;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Exception thrown when OPM validation fails.
/// </summary>
public class OpmValidationException : Exception
{
    /// <summary>
    /// Gets the validation result containing the errors and warnings.
    /// </summary>
    public OpmValidationResult ValidationResult { get; }

    /// <summary>
    /// Creates a new OpmValidationException with the specified message and validation result.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="validationResult">The validation result containing errors and warnings.</param>
    public OpmValidationException(string message, OpmValidationResult validationResult)
        : base(FormatMessage(message, validationResult))
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }

    /// <summary>
    /// Creates a new OpmValidationException with the specified message, validation result, and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="validationResult">The validation result containing errors and warnings.</param>
    /// <param name="innerException">The inner exception.</param>
    public OpmValidationException(string message, OpmValidationResult validationResult, Exception innerException)
        : base(FormatMessage(message, validationResult), innerException)
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }

    private static string FormatMessage(string message, OpmValidationResult result)
    {
        if (result == null || result.ErrorCount == 0)
            return message;

        var sb = new StringBuilder();
        sb.Append(message);
        sb.Append($" ({result.ErrorCount} error(s)");

        if (result.WarningCount > 0)
            sb.Append($", {result.WarningCount} warning(s)");

        sb.Append(")");

        // Add first few errors to the message
        var errorCount = 0;
        foreach (var error in result.Errors)
        {
            if (errorCount >= 3)
            {
                sb.Append($"\n  ... and {result.ErrorCount - 3} more error(s)");
                break;
            }

            sb.Append($"\n  - {error.Message} (at {error.Path})");
            errorCount++;
        }

        return sb.ToString();
    }
}
