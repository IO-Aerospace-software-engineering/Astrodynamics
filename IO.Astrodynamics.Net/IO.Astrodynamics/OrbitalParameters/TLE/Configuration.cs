namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// Configuration record for TLE generation containing all necessary parameters
/// </summary>
public record Configuration(
    ushort NoradId,
    string Name,
    string CosparId,
    ushort RevolutionsAtEpoch = 0,
    Classification Classification = Classification.Unclassified,
    double FirstDerivativeMeanMotion = 0.0,
    double SecondDerivativeMeanMotion = 0.0,
    double BstarDragTerm = 0.0001,
    double Tolerance = 1.0,
    ushort MaxIterations = 15,
    ushort ElementSetNumber = 9999
);