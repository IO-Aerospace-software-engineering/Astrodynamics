namespace IO.Astrodynamics.OrbitalParameters.TLE;

/// <summary>
/// Configuration record for TLE generation containing all necessary parameters
/// </summary>
/// <param name="NoradId">
/// NORAD catalog number, between 0 and <see cref="NoradCatalogNumber.MaxValue"/> (339999).
/// Values above 99999 are written using the Alpha-5 convention.
/// </param>
/// <param name="Name">Satellite name.</param>
/// <param name="CosparId">COSPAR international designator in TLE form (6 to 8 characters, e.g. "98067A"), or null or blank
/// for none: columns 10-17 of line 1 are then blank, as <see cref="TLE.Create"/> writes them (#604, decided by S. Guillet,
/// 2026-11-18; before 11.0.0 the fit threw <see cref="System.InvalidOperationException"/>). A non-blank value of another
/// length is not a designator: <c>ToTLE</c> then throws <see cref="System.InvalidOperationException"/>, its fit failing.</param>
/// <param name="RevolutionsAtEpoch">Revolution number at epoch, between 0 and 99999.</param>
/// <param name="Classification">Security classification of the object.</param>
/// <param name="FirstDerivativeMeanMotion">First derivative of the mean motion, in rev/day².</param>
/// <param name="SecondDerivativeMeanMotion">Second derivative of the mean motion, in rev/day³.</param>
/// <param name="BstarDragTerm">BSTAR drag term, in 1/earth radii.</param>
/// <param name="Tolerance">Fitting tolerance.</param>
/// <param name="MaxIterations">Maximum number of fitting iterations.</param>
/// <param name="ElementSetNumber">Element set number, between 0 and 9999 (default: <see cref="TLE.DefaultElementSetNumber"/>,
/// 999, as <see cref="TLE.Create"/> and <c>Omm.ToTle</c>; 9999 before 11.0.0, #606).</param>
public record Configuration(
    int NoradId,
    string Name,
    string CosparId,
    int RevolutionsAtEpoch = 0,
    Classification Classification = Classification.Unclassified,
    double FirstDerivativeMeanMotion = 0.0,
    double SecondDerivativeMeanMotion = 0.0,
    double BstarDragTerm = 0.0001,
    double Tolerance = 1.0,
    ushort MaxIterations = 15,
    int ElementSetNumber = TLE.DefaultElementSetNumber
);
