// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Represents the result of validating an OPM document.
/// </summary>
public sealed class OpmValidationResult
{
    private readonly List<OpmValidationError> _issues;

    /// <summary>
    /// Gets all validation issues (errors, warnings, and info).
    /// </summary>
    public IReadOnlyList<OpmValidationError> Issues => _issues.AsReadOnly();

    /// <summary>
    /// Gets all error-level issues.
    /// </summary>
    public IEnumerable<OpmValidationError> Errors =>
        _issues.Where(i => i.Severity == OpmValidationSeverity.Error);

    /// <summary>
    /// Gets all warning-level issues.
    /// </summary>
    public IEnumerable<OpmValidationError> Warnings =>
        _issues.Where(i => i.Severity == OpmValidationSeverity.Warning);

    /// <summary>
    /// Gets all info-level issues.
    /// </summary>
    public IEnumerable<OpmValidationError> Infos =>
        _issues.Where(i => i.Severity == OpmValidationSeverity.Info);

    /// <summary>
    /// Gets whether the OPM is valid (no errors, warnings are allowed).
    /// </summary>
    public bool IsValid => !Errors.Any();

    /// <summary>
    /// Gets whether the OPM is strictly valid (no errors and no warnings).
    /// </summary>
    public bool IsStrictlyValid => !Errors.Any() && !Warnings.Any();

    /// <summary>
    /// Gets the count of errors.
    /// </summary>
    public int ErrorCount => _issues.Count(i => i.Severity == OpmValidationSeverity.Error);

    /// <summary>
    /// Gets the count of warnings.
    /// </summary>
    public int WarningCount => _issues.Count(i => i.Severity == OpmValidationSeverity.Warning);

    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    public OpmValidationResult()
    {
        _issues = new List<OpmValidationError>();
    }

    /// <summary>
    /// Creates a new validation result with the specified issues.
    /// </summary>
    public OpmValidationResult(IEnumerable<OpmValidationError> issues)
    {
        _issues = issues?.ToList() ?? new List<OpmValidationError>();
    }

    /// <summary>
    /// Adds an issue to the validation result.
    /// </summary>
    public void AddIssue(OpmValidationError issue)
    {
        _issues.Add(issue);
    }

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string code, string message, string path)
    {
        _issues.Add(OpmValidationError.Error(code, message, path));
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    public void AddWarning(string code, string message, string path)
    {
        _issues.Add(OpmValidationError.Warning(code, message, path));
    }

    /// <summary>
    /// Adds an info message to the validation result.
    /// </summary>
    public void AddInfo(string code, string message, string path)
    {
        _issues.Add(OpmValidationError.Info(code, message, path));
    }

    /// <summary>
    /// Creates a successful validation result with no issues.
    /// </summary>
    public static OpmValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static OpmValidationResult Failure(string code, string message, string path)
    {
        var result = new OpmValidationResult();
        result.AddError(code, message, path);
        return result;
    }

    public override string ToString()
    {
        if (IsStrictlyValid)
            return "Validation passed with no issues.";

        if (IsValid)
            return $"Validation passed with {WarningCount} warning(s).";

        return $"Validation failed with {ErrorCount} error(s) and {WarningCount} warning(s).";
    }
}
