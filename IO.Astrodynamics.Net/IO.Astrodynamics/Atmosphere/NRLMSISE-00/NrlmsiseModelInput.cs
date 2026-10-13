// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00
{
    /// <summary>
    /// The inputs of <see cref="NRLMSISE00"/> in the internal units of the model: altitude in km, geodetic latitude and
    /// longitude in degrees.
    /// </summary>
    /// <remarks>
    /// A value type passed by <c>in</c>, so the model builds its working copies of the inputs without allocating, where
    /// the public <see cref="NrlmsiseInput"/> record allocated one per copy. The conversion from SI units performs the
    /// operations the public methods performed on the record, so the model sees the same doubles.
    /// </remarks>
    internal readonly struct NrlmsiseModelInput
    {
        internal NrlmsiseModelInput(int doy, double sec, double alt, double gLat, double gLong, double lst,
            double f107A, double f107, double ap, ApArray apA)
        {
            Doy = doy;
            Sec = sec;
            Alt = alt;
            GLat = gLat;
            GLong = gLong;
            Lst = lst;
            F107A = f107A;
            F107 = f107;
            Ap = ap;
            ApA = apA;
        }

        /// <summary>Day of year (1-366).</summary>
        internal int Doy { get; }

        /// <summary>Seconds in day (UT).</summary>
        internal double Sec { get; }

        /// <summary>Altitude, in km.</summary>
        internal double Alt { get; }

        /// <summary>Geodetic latitude, in degrees.</summary>
        internal double GLat { get; }

        /// <summary>Geodetic longitude, in degrees.</summary>
        internal double GLong { get; }

        /// <summary>Local apparent solar time, in hours.</summary>
        internal double Lst { get; }

        /// <summary>81 day average of F10.7 flux.</summary>
        internal double F107A { get; }

        /// <summary>Daily F10.7 flux for previous day.</summary>
        internal double F107 { get; }

        /// <summary>Daily magnetic index.</summary>
        internal double Ap { get; }

        /// <summary>The 3 hr AP indices, read when switch 9 is -1.</summary>
        internal ApArray ApA { get; }

        /// <summary>
        /// The inputs of a public input record, from SI units (m, rad) to the internal units (km, degrees).
        /// </summary>
        internal static NrlmsiseModelInput FromSi(NrlmsiseInput input)
        {
            return FromSi(input.Doy, input.Sec, input.Alt, input.GLat, input.GLong, input.Lst, input.F107A, input.F107,
                input.Ap, input.ApA);
        }

        /// <summary>
        /// The inputs from SI units: altitude in m, latitude and longitude in rad.
        /// </summary>
        internal static NrlmsiseModelInput FromSi(int doy, double sec, double alt, double gLat, double gLong, double lst,
            double f107A, double f107, double ap, ApArray apA)
        {
            return new NrlmsiseModelInput(doy, sec, alt / 1000.0, gLat * Constants.Rad2Deg, gLong * Constants.Rad2Deg,
                lst, f107A, f107, ap, apA);
        }

        /// <summary>The same inputs at another altitude, in km.</summary>
        internal NrlmsiseModelInput WithAlt(double alt)
        {
            return new NrlmsiseModelInput(Doy, Sec, alt, GLat, GLong, Lst, F107A, F107, Ap, ApA);
        }
    }
}
