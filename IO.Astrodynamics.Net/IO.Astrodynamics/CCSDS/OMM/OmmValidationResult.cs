// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents the result of validating an OMM document.
/// </summary>
public sealed class OmmValidationResult
{
    private readonly List<OmmValidationError> _issues;

    /// <summary>
    /// Gets all validation issues (errors, warnings, and info).
    /// </summary>
    public IReadOnlyList<OmmValidationError> Issues => _issues.AsReadOnly();

    /// <summary>
    /// Gets all error-level issues.
    /// </summary>
    public IEnumerable<OmmValidationError> Errors =>
        _issues.Where(i => i.Severity == OmmValidationSeverity.Error);

    /// <summary>
    /// Gets all warning-level issues.
    /// </summary>
    public IEnumerable<OmmValidationError> Warnings =>
        _issues.Where(i => i.Severity == OmmValidationSeverity.Warning);

    /// <summary>
    /// Gets all info-level issues.
    /// </summary>
    public IEnumerable<OmmValidationError> Infos =>
        _issues.Where(i => i.Severity == OmmValidationSeverity.Info);

    /// <summary>
    /// Gets whether the OMM is valid (no errors, warnings are allowed).
    /// </summary>
    public bool IsValid => !Errors.Any();

    /// <summary>
    /// Gets whether the OMM is strictly valid (no errors and no warnings).
    /// </summary>
    public bool IsStrictlyValid => !Errors.Any() && !Warnings.Any();

    /// <summary>
    /// Gets the count of errors.
    /// </summary>
    public int ErrorCount => _issues.Count(i => i.Severity == OmmValidationSeverity.Error);

    /// <summary>
    /// Gets the count of warnings.
    /// </summary>
    public int WarningCount => _issues.Count(i => i.Severity == OmmValidationSeverity.Warning);

    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    public OmmValidationResult()
    {
        _issues = new List<OmmValidationError>();
    }

    /// <summary>
    /// Creates a new validation result with the specified issues.
    /// </summary>
    public OmmValidationResult(IEnumerable<OmmValidationError> issues)
    {
        _issues = issues?.ToList() ?? new List<OmmValidationError>();
    }

    /// <summary>
    /// Adds an issue to the validation result.
    /// </summary>
    public void AddIssue(OmmValidationError issue)
    {
        _issues.Add(issue);
    }

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string code, string message, string path)
    {
        _issues.Add(OmmValidationError.Error(code, message, path));
    }

    /// <summary>
    /// Adds a warning to the validation result.
    /// </summary>
    public void AddWarning(string code, string message, string path)
    {
        _issues.Add(OmmValidationError.Warning(code, message, path));
    }

    /// <summary>
    /// Adds an info message to the validation result.
    /// </summary>
    public void AddInfo(string code, string message, string path)
    {
        _issues.Add(OmmValidationError.Info(code, message, path));
    }

    /// <summary>
    /// Creates a successful validation result with no issues.
    /// </summary>
    public static OmmValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static OmmValidationResult Failure(string code, string message, string path)
    {
        var result = new OmmValidationResult();
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
