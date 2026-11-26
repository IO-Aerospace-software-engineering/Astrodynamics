// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.CCSDS.CDM;

public enum CdmValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record CdmValidationError(
    CdmValidationSeverity Severity,
    string Code,
    string Message,
    string Path);

public sealed class CdmValidationResult
{
    private readonly List<CdmValidationError> _issues = new();

    public IReadOnlyList<CdmValidationError> Issues => _issues;

    public IEnumerable<CdmValidationError> Errors => _issues.Where(issue => issue.Severity == CdmValidationSeverity.Error);

    public IEnumerable<CdmValidationError> Warnings => _issues.Where(issue => issue.Severity == CdmValidationSeverity.Warning);

    public bool IsValid => !Errors.Any();

    internal void AddIssue(CdmValidationError issue)
    {
        _issues.Add(issue);
    }

    internal void AddError(string code, string message, string path)
    {
        AddIssue(new CdmValidationError(CdmValidationSeverity.Error, code, message, path));
    }

    internal void AddWarning(string code, string message, string path)
    {
        AddIssue(new CdmValidationError(CdmValidationSeverity.Warning, code, message, path));
    }

    public static CdmValidationResult Failure(string code, string message, string path)
    {
        var result = new CdmValidationResult();
        result.AddError(code, message, path);
        return result;
    }
}

public sealed class CdmParseException : CcsdsFormatException
{
    public CdmParseException(string message)
        : base(message)
    {
    }

    public CdmParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A CDM that the checks of <see cref="Cdm.Load"/>, <see cref="Cdm.Parse(string, CcsdsReadOptions)"/> and their siblings,
/// or of <see cref="Cdm.Save(string, CcsdsWriteOptions)"/> and <see cref="Cdm.ToXml"/>, refuse: the NDM/XML schema or the
/// content (R22 of #668, IC-24 of lot L7), as <see cref="OMM.OmmValidationException"/> and
/// <see cref="OPM.OpmValidationException"/> for theirs.
/// </summary>
public sealed class CdmValidationException : CcsdsFormatException
{
    /// <summary>A CDM refused by its checks.</summary>
    /// <param name="message">The message.</param>
    /// <param name="validationResult">The result of the check that failed.</param>
    public CdmValidationException(string message, CdmValidationResult validationResult)
        : base(FormatMessage(message, validationResult))
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }

    /// <summary>The result of the check that failed, its errors and warnings.</summary>
    public CdmValidationResult ValidationResult { get; }

    // The message, then each error, as the OMM and OPM validation exceptions write theirs
    private static string FormatMessage(string message, CdmValidationResult? result)
    {
        if (result == null || !result.Errors.Any())
            return message;

        return message + Environment.NewLine + string.Join(Environment.NewLine,
            result.Errors.Select(error => $"  {error.Code}: {error.Message} ({error.Path})"));
    }
}
