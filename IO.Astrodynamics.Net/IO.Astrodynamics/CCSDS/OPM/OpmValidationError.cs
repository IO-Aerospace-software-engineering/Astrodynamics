// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Severity level for OPM validation issues.
/// </summary>
public enum OpmValidationSeverity
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
/// Represents a single validation error or warning for an OPM document.
/// </summary>
public sealed class OpmValidationError
{
    /// <summary>
    /// Gets the severity level of this validation issue.
    /// </summary>
    public OpmValidationSeverity Severity { get; }

    /// <summary>
    /// Gets the error code for this validation issue.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable message describing the issue.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the path to the element that caused the issue (e.g., "Data.StateVector.X").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Creates a new validation error.
    /// </summary>
    public OpmValidationError(OpmValidationSeverity severity, string code, string message, string path)
    {
        Severity = severity;
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Creates an error-level validation issue.
    /// </summary>
    public static OpmValidationError Error(string code, string message, string path) =>
        new(OpmValidationSeverity.Error, code, message, path);

    /// <summary>
    /// Creates a warning-level validation issue.
    /// </summary>
    public static OpmValidationError Warning(string code, string message, string path) =>
        new(OpmValidationSeverity.Warning, code, message, path);

    /// <summary>
    /// Creates an info-level validation issue.
    /// </summary>
    public static OpmValidationError Info(string code, string message, string path) =>
        new(OpmValidationSeverity.Info, code, message, path);

    public override string ToString() => $"[{Severity}] {Code}: {Message} (at {Path})";
}
