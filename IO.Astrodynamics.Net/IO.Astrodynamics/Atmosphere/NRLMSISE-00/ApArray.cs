namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00
{
    /// <summary>
    /// Record containing magnetic activity values.
    /// </summary>
    /// <remarks>
    /// Array containing the following magnetic values:
    ///  0 : daily AP
    ///  1 : 3 hr AP index for current time
    ///  2 : 3 hr AP index for 3 hrs before current time
    ///  3 : 3 hr AP index for 6 hrs before current time
    ///  4 : 3 hr AP index for 9 hrs before current time
    ///  5 : Average of eight 3 hr AP indices from 12 to 33 hrs prior to current time
    ///  6 : Average of eight 3 hr AP indices from 36 to 57 hrs prior to current time
    /// </remarks>
    public record ApArray
    {
        /// <summary>
        /// Magnetic activity values (7 elements).
        /// </summary>
        public double[] A { get; init; } = new double[7];

        /// <summary>
        /// Creates a default ApArray with nominal quiet geomagnetic conditions (all values = 4.0).
        /// </summary>
        public static ApArray Default => new ApArray
        {
            A = new double[] { 4.0, 4.0, 4.0, 4.0, 4.0, 4.0, 4.0 }
        };
    }
}
