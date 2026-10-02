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

public sealed class CdmParseException : Exception
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
