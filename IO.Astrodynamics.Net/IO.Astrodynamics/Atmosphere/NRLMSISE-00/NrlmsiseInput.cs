namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00
{
    /// <summary>
    /// Input parameters for NRLMSISE-00 model.
    /// </summary>
    /// <remarks>
    /// NOTES ON INPUT VARIABLES:
    ///   UT, Local Time, and Longitude are used independently in the
    ///   model and are not of equal importance for every situation.
    ///   For the most physically realistic calculation these three
    ///   variables should be consistent (lst=sec/3600 + g_long/15).
    ///   The Equation of Time departures from the above formula
    ///   for apparent local time can be included if available but
    ///   are of minor importance.
    ///
    ///   f107 and f107A values used to generate the model correspond
    ///   to the 10.7 cm radio flux at the actual distance of the Earth
    ///   from the Sun rather than the radio flux at 1 AU.
    ///
    ///   f107, f107A, and ap effects are neither large nor well
    ///   established below 80 km and these parameters should be set to
    ///   150., 150., and 4. respectively.
    /// </remarks>
    public record NrlmsiseInput
    {
        /// <summary>
        /// Year (currently ignored).
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Day of year (1-366).
        /// </summary>
        public int Doy { get; set; }

        /// <summary>
        /// Seconds in day (UT).
        /// </summary>
        public double Sec { get; set; }

        /// <summary>
        /// Altitude in kilometers.
        /// </summary>
        public double Alt { get; set; }

        /// <summary>
        /// Geodetic latitude in degrees.
        /// </summary>
        public double GLat { get; set; }

        /// <summary>
        /// Geodetic longitude in degrees.
        /// </summary>
        public double GLong { get; set; }

        /// <summary>
        /// Local apparent solar time in hours.
        /// </summary>
        /// <remarks>
        /// See notes about relationship with Sec and GLong.
        /// </remarks>
        public double Lst { get; set; }

        /// <summary>
        /// 81 day average of F10.7 flux (centered on doy).
        /// </summary>
        public double F107A { get; set; }

        /// <summary>
        /// Daily F10.7 flux for previous day.
        /// </summary>
        public double F107 { get; set; }

        /// <summary>
        /// Magnetic index (daily).
        /// </summary>
        public double Ap { get; set; }

        /// <summary>
        /// Array of AP indices (optional, used when flags.switches[9] == -1).
        /// </summary>
        public ApArray ApA { get; set; }
    }
}
