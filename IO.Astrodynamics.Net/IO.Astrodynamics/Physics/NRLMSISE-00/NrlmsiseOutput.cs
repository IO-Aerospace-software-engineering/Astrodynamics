namespace IO.Astrodynamics.Physics.NRLMSISE_00
{
    /// <summary>
    /// Output values from NRLMSISE-00 model.
    /// </summary>
    /// <remarks>
    /// OUTPUT VARIABLES:
    ///   d[0] - HE NUMBER DENSITY(CM-3)
    ///   d[1] - O NUMBER DENSITY(CM-3)
    ///   d[2] - N2 NUMBER DENSITY(CM-3)
    ///   d[3] - O2 NUMBER DENSITY(CM-3)
    ///   d[4] - AR NUMBER DENSITY(CM-3)
    ///   d[5] - TOTAL MASS DENSITY(GM/CM3) [includes d[8] in gtd7d]
    ///   d[6] - H NUMBER DENSITY(CM-3)
    ///   d[7] - N NUMBER DENSITY(CM-3)
    ///   d[8] - Anomalous oxygen NUMBER DENSITY(CM-3)
    ///   t[0] - EXOSPHERIC TEMPERATURE
    ///   t[1] - TEMPERATURE AT ALT
    ///
    ///   O, H, and N are set to zero below 72.5 km
    ///
    ///   t[0], Exospheric temperature, is set to global average for
    ///   altitudes below 120 km. The 120 km gradient is left at global
    ///   average value for altitudes below 72 km.
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
        /// Densities (9 elements).
        /// Indices 0-4, 6-8 are species densities, index 5 is total mass density.
        /// </summary>
        public double[] D { get; set; } = new double[9];

        /// <summary>
        /// Temperatures (2 elements).
        /// Index 0 is exospheric temperature, index 1 is temperature at altitude.
        /// </summary>
        public double[] T { get; set; } = new double[2];
    }
}
