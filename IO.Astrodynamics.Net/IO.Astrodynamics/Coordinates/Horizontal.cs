using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Coordinates
{
    /// <summary>
    /// Horizontal coordinates of a target seen from a site: the azimuth and the elevation (rad) and the range (m), at an epoch.
    /// </summary>
    public readonly record struct Horizontal
    {
        /// <summary>The azimuth, rad, in [0, 2π).</summary>
        public double Azimuth { get; init; }

        /// <summary>The elevation above the local horizontal, rad.</summary>
        public double Elevation { get; init; }

        /// <summary>The range, m.</summary>
        public double Range { get; init; }

        /// <summary>The epoch of the coordinates.</summary>
        public Time Epoch { get; init; }

        /// <summary>
        /// The coordinates of an azimuth, an elevation and a range at an epoch (R20 of #668: camelCase parameters, before
        /// 11.0.0 the PascalCase parameters of a positional record).
        /// </summary>
        /// <param name="azimuth">The azimuth, rad.</param>
        /// <param name="elevation">The elevation, rad.</param>
        /// <param name="range">The range, m.</param>
        /// <param name="epoch">The epoch of the coordinates.</param>
        public Horizontal(double azimuth, double elevation, double range, Time epoch)
        {
            Azimuth = azimuth;
            Elevation = elevation;
            Range = range;
            Epoch = epoch;
        }

        /// <summary>Deconstruct into the azimuth, the elevation, the range and the epoch.</summary>
        /// <param name="azimuth">The azimuth, rad.</param>
        /// <param name="elevation">The elevation, rad.</param>
        /// <param name="range">The range, m.</param>
        /// <param name="epoch">The epoch of the coordinates.</param>
        public void Deconstruct(out double azimuth, out double elevation, out double range, out Time epoch)
        {
            azimuth = Azimuth;
            elevation = Elevation;
            range = Range;
            epoch = Epoch;
        }
    }
}
