// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Severity level for OMM validation issues.
/// </summary>
public enum OmmValidationSeverity
{
    /// <summary>
    /// Validation error - data is invalid and cannot be used reliably.
    /// </summary>
    Error,

    /// <summary>
    /// Validation warning - data may have issues but can still be processed.
    /// </summary>
    Warning,

    /// <summary>
    /// Informational message - data is valid but has noteworthy characteristics.
    /// </summary>
    Info
}

/// <summary>
/// Represents a single validation error or warning for an OMM document.
/// </summary>
public sealed class OmmValidationError
{
    /// <summary>
    /// Gets the severity level of this validation issue.
    /// </summary>
    public OmmValidationSeverity Severity { get; }

    /// <summary>
    /// Gets the error code for this validation issue.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable message describing the issue.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the path to the element that caused the issue (e.g., "Data.MeanElements.Eccentricity").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Creates a new validation error.
    /// </summary>
    public OmmValidationError(OmmValidationSeverity severity, string code, string message, string path)
    {
        Severity = severity;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Creates an error-level validation issue.
    /// </summary>
    public static OmmValidationError Error(string code, string message, string path) =>
        new(OmmValidationSeverity.Error, code, message, path);

    /// <summary>
    /// Creates a warning-level validation issue.
    /// </summary>
    public static OmmValidationError Warning(string code, string message, string path) =>
        new(OmmValidationSeverity.Warning, code, message, path);

    /// <summary>
    /// Creates an info-level validation issue.
    /// </summary>
    public static OmmValidationError Info(string code, string message, string path) =>
        new(OmmValidationSeverity.Info, code, message, path);

    public override string ToString() => $"[{Severity}] {Code}: {Message} (at {Path})";
}
