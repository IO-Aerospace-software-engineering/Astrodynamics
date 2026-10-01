// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)
#nullable enable
using System;

namespace IO.Astrodynamics.CCSDS.CDM;

public enum CdmObjectRole
{
    Object1,
    Object2
}

public enum CdmScreenVolumeFrame
{
    Rtn,
    Tvn
}

public enum CdmScreenVolumeShape
{
    Ellipsoid,
    Box
}

public enum CdmReferenceFrame
{
    Eme2000,
    Gcrf,
    Itrf
}

public enum CdmCovarianceMethod
{
    Calculated,
    Default
}

public enum CdmManeuverable
{
    Yes,
    No,
    NotApplicable
}

internal static class CdmEnumExtensions
{
    public static string ToSchemaValue(this CdmObjectRole value)
    {
        return value switch
        {
            CdmObjectRole.Object1 => "OBJECT1",
            CdmObjectRole.Object2 => "OBJECT2",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static string ToSchemaValue(this CdmScreenVolumeFrame value)
    {
        return value switch
        {
            CdmScreenVolumeFrame.Rtn => "RTN",
            CdmScreenVolumeFrame.Tvn => "TVN",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static string ToSchemaValue(this CdmScreenVolumeShape value)
    {
        return value switch
        {
            CdmScreenVolumeShape.Ellipsoid => "ELLIPSOID",
            CdmScreenVolumeShape.Box => "BOX",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static string ToSchemaValue(this CdmReferenceFrame value)
    {
        return value switch
        {
            CdmReferenceFrame.Eme2000 => "EME2000",
            CdmReferenceFrame.Gcrf => "GCRF",
            CdmReferenceFrame.Itrf => "ITRF",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static string ToSchemaValue(this CdmCovarianceMethod value)
    {
        return value switch
        {
            CdmCovarianceMethod.Calculated => "CALCULATED",
            CdmCovarianceMethod.Default => "DEFAULT",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static string ToSchemaValue(this CdmManeuverable value)
    {
        return value switch
        {
            CdmManeuverable.Yes => "YES",
            CdmManeuverable.No => "NO",
            CdmManeuverable.NotApplicable => "N/A",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static CdmObjectRole ParseObjectRole(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "OBJECT1" => CdmObjectRole.Object1,
            "OBJECT2" => CdmObjectRole.Object2,
            _ => throw new FormatException($"Unsupported CDM object role '{value}'.")
        };
    }

    public static CdmScreenVolumeFrame ParseScreenVolumeFrame(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "RTN" => CdmScreenVolumeFrame.Rtn,
            "TVN" => CdmScreenVolumeFrame.Tvn,
            _ => throw new FormatException($"Unsupported CDM screen volume frame '{value}'.")
        };
    }

    public static CdmScreenVolumeShape ParseScreenVolumeShape(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "ELLIPSOID" => CdmScreenVolumeShape.Ellipsoid,
            "BOX" => CdmScreenVolumeShape.Box,
            _ => throw new FormatException($"Unsupported CDM screen volume shape '{value}'.")
        };
    }

    public static CdmReferenceFrame ParseReferenceFrame(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "EME2000" => CdmReferenceFrame.Eme2000,
            "GCRF" => CdmReferenceFrame.Gcrf,
            "ITRF" => CdmReferenceFrame.Itrf,
            _ => throw new FormatException($"Unsupported CDM reference frame '{value}'.")
        };
    }

    public static CdmCovarianceMethod ParseCovarianceMethod(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "CALCULATED" => CdmCovarianceMethod.Calculated,
            "DEFAULT" => CdmCovarianceMethod.Default,
            _ => throw new FormatException($"Unsupported CDM covariance method '{value}'.")
        };
    }

    public static CdmManeuverable ParseManeuverable(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "YES" => CdmManeuverable.Yes,
            "NO" => CdmManeuverable.No,
            "N/A" => CdmManeuverable.NotApplicable,
            _ => throw new FormatException($"Unsupported CDM maneuverable value '{value}'.")
        };
    }

    public static string ToYesNoString(bool value) => value ? "YES" : "NO";

    public static bool ParseYesNo(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "YES" => true,
            "NO" => false,
            _ => throw new FormatException($"Unsupported YES/NO value '{value}'.")
        };
    }
}
