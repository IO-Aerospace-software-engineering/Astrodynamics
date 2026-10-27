// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;

namespace IO.Astrodynamics.Body
{
    /// <summary>
    /// Represents the parameters for a geopotential model.
    /// </summary>
    /// <remarks>
    /// The coefficients of a model are normalized to its own gravitational parameter and reference radius, which the
    /// coefficient file does not carry. Without them (the constructors without constants), the harmonics of the Earth
    /// (NAIF 399) are evaluated with those of EGM2008 (<see cref="Egm2008GravitationalParameter"/>,
    /// <see cref="Egm2008ReferenceRadius"/>), the model shipped with the library, and the harmonics of any other body
    /// with the GM and the equatorial radius of its kernels. The central term takes the GM of the kernels by default, or
    /// the GM of the model with <see cref="CentralTerm"/> set to
    /// <see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/>.
    /// </remarks>
    public class GeopotentialModelParameters
    {
        /// <summary>
        /// The gravitational parameter of EGM2008, 3.986004415e14 m³/s², to which its coefficients are normalized: the
        /// <c>earth_gravity_constant</c> of the ICGEM distribution of the model (Pavlis, Holmes, Kenyon and Factor,
        /// 2012, Journal of Geophysical Research 117, B04406; verified by S. Guillet, 2026-10-23). A TT-compatible value.
        /// </summary>
        internal const double Egm2008GravitationalParameter = 3.986004415e14;

        /// <summary>
        /// The reference radius of EGM2008, 6378136.3 m, to which its coefficients are normalized: the <c>radius</c> of
        /// the ICGEM distribution of the model (Pavlis et al., 2012; verified by S. Guillet, 2026-10-23).
        /// </summary>
        internal const double Egm2008ReferenceRadius = 6378136.3;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeopotentialModelParameters"/> class with the specified geopotential model path and degree.
        /// </summary>
        /// <param name="geopotentialModelPath">The file path to the geopotential model.</param>
        /// <param name="geopotentialDegree">The degree of the geopotential model. Default is 60.</param>
        public GeopotentialModelParameters(string geopotentialModelPath, ushort geopotentialDegree = 60) : this(
            new FileStream(geopotentialModelPath, FileMode.Open, FileAccess.Read, FileShare.Read),
            geopotentialDegree)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeopotentialModelParameters"/> class with the specified geopotential model stream and degree.
        /// </summary>
        /// <param name="geopotentialModelStream">The stream of the geopotential model.</param>
        /// <param name="geopotentialDegree">The degree of the geopotential model. Default is 60.</param>
        public GeopotentialModelParameters(Stream geopotentialModelStream, ushort geopotentialDegree = 60)
        {
            GeopotentialModelPath = new StreamReader(geopotentialModelStream);
            GeopotentialDegree = geopotentialDegree;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeopotentialModelParameters"/> class for a model whose
        /// coefficients are normalized to the given gravitational parameter and reference radius.
        /// </summary>
        /// <param name="geopotentialModelPath">The file path to the geopotential model.</param>
        /// <param name="geopotentialDegree">The degree of the geopotential model.</param>
        /// <param name="gravitationalParameter">The gravitational parameter of the model, in m³/s².</param>
        /// <param name="referenceRadius">The reference radius of the model, in m.</param>
        /// <exception cref="ArgumentOutOfRangeException">A constant is not finite and positive.</exception>
        public GeopotentialModelParameters(string geopotentialModelPath, ushort geopotentialDegree,
            double gravitationalParameter, double referenceRadius) : this(
            new FileStream(geopotentialModelPath, FileMode.Open, FileAccess.Read, FileShare.Read), geopotentialDegree,
            gravitationalParameter, referenceRadius)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeopotentialModelParameters"/> class for a model whose
        /// coefficients are normalized to the given gravitational parameter and reference radius.
        /// </summary>
        /// <param name="geopotentialModelStream">The stream of the geopotential model.</param>
        /// <param name="geopotentialDegree">The degree of the geopotential model.</param>
        /// <param name="gravitationalParameter">The gravitational parameter of the model, in m³/s².</param>
        /// <param name="referenceRadius">The reference radius of the model, in m.</param>
        /// <exception cref="ArgumentOutOfRangeException">A constant is not finite and positive.</exception>
        public GeopotentialModelParameters(Stream geopotentialModelStream, ushort geopotentialDegree,
            double gravitationalParameter, double referenceRadius) : this(geopotentialModelStream, geopotentialDegree)
        {
            GravitationalParameter = Positive(gravitationalParameter, nameof(gravitationalParameter));
            ReferenceRadius = Positive(referenceRadius, nameof(referenceRadius));
        }

        /// <summary>
        /// Gets the stream reader for the geopotential model.
        /// </summary>
        public StreamReader GeopotentialModelPath { get; }

        /// <summary>
        /// Gets the degree of the geopotential model.
        /// </summary>
        public ushort GeopotentialDegree { get; }

        /// <summary>
        /// Gets the gravitational parameter of the model, in m³/s², or null when none was given (see the remarks of
        /// the class).
        /// </summary>
        public double? GravitationalParameter { get; }

        /// <summary>
        /// Gets the reference radius of the model, in m, or null when none was given (see the remarks of the class).
        /// </summary>
        public double? ReferenceRadius { get; }

        /// <summary>
        /// Gets or initializes the gravitational parameter of the central term of the field: the GM of the body
        /// (<see cref="GeopotentialCentralTerm.BodyGravitationalParameter"/>, the default), or the GM of the model,
        /// with which the harmonics are evaluated (<see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/>),
        /// so that the whole field takes the constants of the model, as GMAT applies a <c>.cof</c> header.
        /// </summary>
        /// <remarks>
        /// The GM of the model is <see cref="GravitationalParameter"/>, or without it EGM2008's for the Earth and the
        /// GM of the kernels for another body (see the remarks of the class): for another body without constants, both
        /// values give the same field. The option changes the field only: <see cref="CelestialItem.GM"/>, the
        /// third-body perturbations and the conversions of orbital parameters keep the GM of the body.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a member of
        /// <see cref="GeopotentialCentralTerm"/>.</exception>
        public GeopotentialCentralTerm CentralTerm
        {
            get => _centralTerm;
            init
            {
                if (!Enum.IsDefined(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(CentralTerm), value,
                        "The central term must be a member of GeopotentialCentralTerm.");
                }

                _centralTerm = value;
            }
        }

        private readonly GeopotentialCentralTerm _centralTerm;

        private static double Positive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, value, "The constant of the model must be finite and positive.");
            }

            return value;
        }
    }
}
