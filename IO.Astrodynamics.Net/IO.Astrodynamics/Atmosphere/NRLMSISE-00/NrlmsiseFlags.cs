namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00
{
    /// <summary>
    /// Switch flags for controlling NRLMSISE-00 model variations.
    /// </summary>
    /// <remarks>
    /// Switches: to turn on and off particular variations use these switches.
    /// 0 is off, 1 is on, and 2 is main effects off but cross terms on.
    ///
    /// Standard values are 0 for switch 0 and 1 for switches 1 to 23.
    /// The arrays sw and swc are set internally based on switches.
    ///
    /// Use <see cref="CreateStandard"/> to create an instance with standard switch values,
    /// or use the default constructor to create an instance with custom initialization.
    /// </remarks>
    public class NrlmsiseFlags
    {
        /// <summary>
        /// Switch control array (24 elements).
        /// </summary>
        /// <remarks>
        /// switches[i]:
        ///  i - explanation
        /// -----------------
        ///  0 - Reserved (previously unit control, now always SI units)
        ///  1 - F10.7 effect on mean
        ///  2 - time independent
        ///  3 - symmetrical annual
        ///  4 - symmetrical semiannual
        ///  5 - asymmetrical annual
        ///  6 - asymmetrical semiannual
        ///  7 - diurnal
        ///  8 - semidiurnal
        ///  9 - daily ap [when this is set to -1 the pointer
        ///                ap_a in NrlmsiseInput must point to an ApArray]
        /// 10 - all UT/long effects
        /// 11 - longitudinal
        /// 12 - UT and mixed UT/long
        /// 13 - mixed AP/UT/LONG
        /// 14 - terdiurnal
        /// 15 - departures from diffusive equilibrium
        /// 16 - all TINF var
        /// 17 - all TLB var
        /// 18 - all TN1 var
        /// 19 - all S var
        /// 20 - all TN2 var
        /// 21 - all NLB var
        /// 22 - all TN3 var
        /// 23 - turbo scale height var
        /// </remarks>
        public int[] Switches { get; set; } = new int[24];

        /// <summary>
        /// Internal switch array (24 elements), set by tselec.
        /// </summary>
        public double[] Sw { get; set; } = new double[24];

        /// <summary>
        /// Internal switch array (24 elements), set by tselec.
        /// </summary>
        public double[] Swc { get; set; } = new double[24];

        /// <summary>
        /// Initializes a new instance of the <see cref="NrlmsiseFlags"/> class.
        /// </summary>
        /// <remarks>
        /// Creates an instance with all switches set to 0. For standard initialization,
        /// use <see cref="CreateStandard"/> instead.
        /// </remarks>
        public NrlmsiseFlags()
        {
            // Arrays are initialized to zeros by default
        }

        /// <summary>
        /// Creates a new instance of <see cref="NrlmsiseFlags"/> with standard switch values.
        /// </summary>
        /// <returns>A new instance with standard values: 0 for switch 0, 1 for switches 1-23.</returns>
        /// <remarks>
        /// This is the recommended way to create flags for normal NRLMSISE-00 usage.
        /// Standard configuration enables all atmospheric variations except switch 0 (reserved).
        /// </remarks>
        public static NrlmsiseFlags CreateStandard()
        {
            var flags = new NrlmsiseFlags();
            flags.Switches[0] = 0;
            for (int i = 1; i < 24; i++)
            {
                flags.Switches[i] = 1;
            }
            return flags;
        }
    }
}
