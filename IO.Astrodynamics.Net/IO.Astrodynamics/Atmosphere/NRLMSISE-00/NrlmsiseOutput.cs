using System.Globalization;

namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00
{
    /// <summary>
    /// Output values from NRLMSISE-00 model using SI units.
    /// </summary>
    /// <remarks>
    /// OUTPUT VARIABLES (SI UNITS):
    ///   d[0] - HE NUMBER DENSITY (m^-3)
    ///   d[1] - O NUMBER DENSITY (m^-3)
    ///   d[2] - N2 NUMBER DENSITY (m^-3)
    ///   d[3] - O2 NUMBER DENSITY (m^-3)
    ///   d[4] - AR NUMBER DENSITY (m^-3)
    ///   d[5] - TOTAL MASS DENSITY (kg/m^3) [includes d[8] in gtd7d]
    ///   d[6] - H NUMBER DENSITY (m^-3)
    ///   d[7] - N NUMBER DENSITY (m^-3)
    ///   d[8] - Anomalous oxygen NUMBER DENSITY (m^-3)
    ///   t[0] - EXOSPHERIC TEMPERATURE (K)
    ///   t[1] - TEMPERATURE AT ALT (K)
    ///
    ///   O, H, and N are set to zero below 72500 m
    ///
    ///   t[0], Exospheric temperature, is set to global average for
    ///   altitudes below 120000 m. The 120000 m gradient is left at global
    ///   average value for altitudes below 72000 m.
    ///
    ///   d[5], TOTAL MASS DENSITY, is NOT the same for subroutines GTD7 and GTD7D
    ///
    ///     SUBROUTINE GTD7 -- d[5] is the sum of the mass densities of the
    ///     species labeled by indices 0-4 and 6-7 in output variable d.
    ///     This includes He, O, N2, O2, Ar, H, and N but does NOT include
    ///     anomalous oxygen (species index 8).
    ///
    ///     SUBROUTINE GTD7D -- d[5] is the "effective total mass density
    ///     for drag" and is the sum of the mass densities of all species
    ///     in this model, INCLUDING anomalous oxygen.
    /// </remarks>
    public record NrlmsiseOutput
    {
        /// <summary>
        /// Densities (9 elements) in SI units.
        /// Indices 0-4, 6-8 are species number densities (m^-3), index 5 is total mass density (kg/m^3).
        /// </summary>
        /// <remarks>
        /// Note: This property uses 'init' (not 'set') to prevent reassignment of the array reference.
        /// However, the NRLMSISE-00 algorithm can still write results into the array elements.
        /// </remarks>
        public double[] D { get; init; } = new double[9];

        /// <summary>
        /// Temperatures (2 elements) in Kelvin.
        /// Index 0 is exospheric temperature, index 1 is temperature at altitude.
        /// </summary>
        /// <remarks>
        /// Note: This property uses 'init' (not 'set') to prevent reassignment of the array reference.
        /// However, the NRLMSISE-00 algorithm can still write results into the array elements.
        /// </remarks>
        public double[] T { get; init; } = new double[2];

        /// <summary>
        /// Returns a human-readable string representation of the NRLMSISE-00 output.
        /// </summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                @"NRLMSISE-00 Atmospheric Model Output:
Densities:
  He (Helium):          {0:E3} m^-3
  O (Oxygen):           {1:E3} m^-3
  N2 (Nitrogen):        {2:E3} m^-3
  O2 (Dioxygen):        {3:E3} m^-3
  Ar (Argon):           {4:E3} m^-3
  Total Mass Density:   {5:E3} kg/m^3
  H (Hydrogen):         {6:E3} m^-3
  N (Nitrogen):         {7:E3} m^-3
  Anomalous O:          {8:E3} m^-3

Temperatures:
  Exospheric:           {9:F2} K
  At Altitude:          {10:F2} K",
                D[0], D[1], D[2], D[3], D[4], D[5], D[6], D[7], D[8], T[0], T[1]);
        }
    }
}