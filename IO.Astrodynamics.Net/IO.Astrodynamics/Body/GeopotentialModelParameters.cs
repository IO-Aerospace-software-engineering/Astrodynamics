// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Physics;

namespace IO.Astrodynamics.Body
{
    /// <summary>
    /// A geopotential model: its coefficients (a model file, a stream, or EGM2008 embedded in the library), the degree of
    /// the field, the constants to which the coefficients are normalized and the GM of the central term. A value:
    /// immutable, equal to another of the same source, degree, constants and central term.
    /// </summary>
    /// <remarks>
    /// The coefficients of a model are normalized to its own gravitational parameter and reference radius, which the
    /// coefficient file does not carry. Without them (the constructors without constants, and <see cref="Egm2008"/>), the
    /// harmonics of the Earth (NAIF 399) are evaluated with those of EGM2008 (<see cref="Egm2008GravitationalParameter"/>,
    /// <see cref="Egm2008ReferenceRadius"/>), the model shipped with the library, and the harmonics of any other body
    /// with the GM and the equatorial radius of its kernels. The central term takes the GM of the kernels by default, or
    /// the GM of the model with <see cref="CentralTerm"/> set to
    /// <see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/>.
    /// <para>
    /// One instance builds any number of bodies, on any thread, and is given to any number of propagations
    /// (<see cref="Propagator.PropagationOptions.Geopotential"/>): the coefficients are read once and shared, and each body
    /// and each propagation gets its own field (<see cref="GeopotentialGravitationalField"/>, not thread-safe), as from a
    /// fresh instance (#674; R06 of #668).
    /// </para>
    /// <para>
    /// Two parameters are equal when they read the same source (the same full path of a model file, EGM2008 embedded, or
    /// streams of the same bytes) with the same degree, constants and central term (R06 of #668: before 11.0.0 a class
    /// compared by reference, which held the <see cref="StreamReader"/> of its file). Two equal parameters give equal
    /// fields; a propagation also takes as one model two parameters of different sources whose fields hold the same
    /// coefficients (<see cref="Propagator.PropagationOptions.Geopotential"/>).
    /// </para>
    /// </remarks>
    public sealed record GeopotentialModelParameters
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
        /// The highest degree of the EGM2008 coefficients embedded in the library (<see cref="Egm2008"/>): 70, those of
        /// <c>EGM2008_to70_TideFree</c>, the file of the tests (<c>Data/SolarSystem</c>), byte for byte.
        /// </summary>
        public const int Egm2008MaximumDegree = 70;

        // The logical name of the embedded EGM2008 coefficients (IO.Astrodynamics.csproj)
        private const string Egm2008Resource = "IO.Astrodynamics.Physics.Models.EGM2008_to70_TideFree";

        // The key of the source: the full path of a model file, the embedded EGM2008, or the bytes of a stream
        private readonly string _path;
        private readonly bool _embedded;
        private readonly byte[] _content;

        // Read once, by the first body or propagation built from these parameters, on any thread; a parse error is kept
        // and thrown again by every body built from them (#674). A copy made by `with` shares it
        private readonly Lazy<GeopotentialModelReader> _modelReader;

        private readonly int _degree;
        private readonly GeopotentialCentralTerm _centralTerm;

        /// <summary>
        /// The parameters of the model file at <paramref name="geopotentialModelPath"/>, to degree
        /// <paramref name="geopotentialDegree"/>.
        /// </summary>
        /// <param name="geopotentialModelPath">The path of the model file, compared by its full path.</param>
        /// <param name="geopotentialDegree">The degree of the field. Default is 60.</param>
        /// <exception cref="ArgumentNullException"><paramref name="geopotentialModelPath"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="geopotentialDegree"/> is negative.</exception>
        /// <exception cref="FileNotFoundException">No file is at <paramref name="geopotentialModelPath"/>.</exception>
        public GeopotentialModelParameters(string geopotentialModelPath, int geopotentialDegree = 60)
            : this(FullPathOf(geopotentialModelPath), false, null, geopotentialDegree)
        {
        }

        /// <summary>
        /// The parameters of the model read from <paramref name="geopotentialModelStream"/>, to degree
        /// <paramref name="geopotentialDegree"/>. The stream is read to its end here and left open; the parameters keep
        /// its bytes, compared by value.
        /// </summary>
        /// <param name="geopotentialModelStream">The stream of the model.</param>
        /// <param name="geopotentialDegree">The degree of the field. Default is 60.</param>
        /// <exception cref="ArgumentNullException"><paramref name="geopotentialModelStream"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="geopotentialDegree"/> is negative.</exception>
        public GeopotentialModelParameters(Stream geopotentialModelStream, int geopotentialDegree = 60)
            : this(null, false, ContentOf(geopotentialModelStream), geopotentialDegree)
        {
        }

        /// <summary>
        /// The parameters of the model file at <paramref name="geopotentialModelPath"/>, whose coefficients are
        /// normalized to the given gravitational parameter and reference radius.
        /// </summary>
        /// <param name="geopotentialModelPath">The path of the model file, compared by its full path.</param>
        /// <param name="geopotentialDegree">The degree of the field.</param>
        /// <param name="gravitationalParameter">The gravitational parameter of the model, in m³/s².</param>
        /// <param name="referenceRadius">The reference radius of the model, in m.</param>
        /// <exception cref="ArgumentOutOfRangeException">A constant is not finite and positive, or
        /// <paramref name="geopotentialDegree"/> is negative.</exception>
        /// <exception cref="FileNotFoundException">No file is at <paramref name="geopotentialModelPath"/>.</exception>
        public GeopotentialModelParameters(string geopotentialModelPath, int geopotentialDegree,
            double gravitationalParameter, double referenceRadius)
            : this(geopotentialModelPath, geopotentialDegree)
        {
            GravitationalParameter = Positive(gravitationalParameter, nameof(gravitationalParameter));
            ReferenceRadius = Positive(referenceRadius, nameof(referenceRadius));
        }

        /// <summary>
        /// The parameters of the model read from <paramref name="geopotentialModelStream"/>, whose coefficients are
        /// normalized to the given gravitational parameter and reference radius. The stream is read to its end here.
        /// </summary>
        /// <param name="geopotentialModelStream">The stream of the model.</param>
        /// <param name="geopotentialDegree">The degree of the field.</param>
        /// <param name="gravitationalParameter">The gravitational parameter of the model, in m³/s².</param>
        /// <param name="referenceRadius">The reference radius of the model, in m.</param>
        /// <exception cref="ArgumentOutOfRangeException">A constant is not finite and positive, or
        /// <paramref name="geopotentialDegree"/> is negative.</exception>
        public GeopotentialModelParameters(Stream geopotentialModelStream, int geopotentialDegree,
            double gravitationalParameter, double referenceRadius)
            : this(geopotentialModelStream, geopotentialDegree)
        {
            GravitationalParameter = Positive(gravitationalParameter, nameof(gravitationalParameter));
            ReferenceRadius = Positive(referenceRadius, nameof(referenceRadius));
        }

        private GeopotentialModelParameters(string path, bool embedded, byte[] content, int degree)
        {
            if (degree < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(GeopotentialDegree), degree,
                    "The degree of a geopotential must be 0 or more.");
            }

            _path = path;
            _embedded = embedded;
            _content = content;
            _degree = degree;
            _modelReader = new Lazy<GeopotentialModelReader>(() => new GeopotentialModelReader(OpenSource()),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// EGM2008, tide free, embedded in the library to degree <see cref="Egm2008MaximumDegree"/> (70), to degree
        /// <paramref name="degree"/>: the coefficients of <c>EGM2008_to70_TideFree</c>, with EGM2008's constants for the
        /// Earth (see the remarks of the class), so that the field equals the field of that file at every degree, without
        /// a file to ship (R06 of #668).
        /// </summary>
        /// <param name="degree">The degree of the field, from 0 to 70.</param>
        /// <returns>The parameters.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="degree"/> is negative or above 70.</exception>
        public static GeopotentialModelParameters Egm2008(int degree)
        {
            if (degree > Egm2008MaximumDegree)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree,
                    $"The embedded EGM2008 goes to degree {Egm2008MaximumDegree}: pass a degree of at most " +
                    $"{Egm2008MaximumDegree}, or the parameters of a model file that goes further.");
            }

            return new GeopotentialModelParameters(null, true, null, degree);
        }

        /// <summary>
        /// The coefficients of the model, read once and shared, read only, by the fields of every body and propagation
        /// built from these parameters (#674).
        /// </summary>
        internal GeopotentialModelReader ModelReader => _modelReader.Value;

        /// <summary>
        /// Gets the degree of the geopotential model (an <see cref="int"/> since 11.0.0, a <see cref="ushort"/> before).
        /// </summary>
        /// <remarks>
        /// At most the highest degree of the model file (70 for EGM2008_to70_TideFree), whose every coefficient (n, m) with
        /// 2 ≤ n ≤ degree must be present. It is checked when a body or a propagation is built from these parameters, which
        /// reads the file once (#674): it throws <see cref="ArgumentOutOfRangeException"/> (<c>maxDegrees</c>) for a degree
        /// above the file, and <see cref="ArgumentException"/> for a missing coefficient (#689; before 11.0.0 both were
        /// read as zeros).
        /// </remarks>
        public int GeopotentialDegree => _degree;

        /// <summary>
        /// Gets the gravitational parameter of the model, in m³/s², or null when none was given (see the remarks of
        /// the class).
        /// </summary>
        public double? GravitationalParameter { get; private init; }

        /// <summary>
        /// Gets the reference radius of the model, in m, or null when none was given (see the remarks of the class).
        /// </summary>
        public double? ReferenceRadius { get; private init; }

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

        /// <summary>
        /// Whether <paramref name="other"/> reads the same source (the same full path, EGM2008 embedded, or streams of the
        /// same bytes) with the same degree, constants and central term.
        /// </summary>
        /// <param name="other">The other parameters.</param>
        /// <returns>Whether both are the same model.</returns>
        public bool Equals(GeopotentialModelParameters other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is not null && _degree == other._degree && _embedded == other._embedded &&
                   string.Equals(_path, other._path, StringComparison.Ordinal) &&
                   (ReferenceEquals(_content, other._content) ||
                    (_content != null && other._content != null && _content.AsSpan().SequenceEqual(other._content))) &&
                   GravitationalParameter.Equals(other.GravitationalParameter) &&
                   ReferenceRadius.Equals(other.ReferenceRadius) && _centralTerm == other._centralTerm;
        }

        /// <summary>
        /// The model in words, for the messages of a propagation: its source, degree, constants and central term.
        /// </summary>
        internal string Description =>
            (_embedded ? "EGM2008 (embedded)" : _path != null ? $"'{_path}'" : $"a stream of {_content.Length} bytes") +
            $" to degree {_degree}" +
            (GravitationalParameter.HasValue ? $", GM {GravitationalParameter.Value:R} m^3/s^2, radius {ReferenceRadius.Value:R} m" : string.Empty) +
            (_centralTerm == GeopotentialCentralTerm.ModelGravitationalParameter ? ", central term of the model" : string.Empty);

        /// <inheritdoc />
        public override int GetHashCode() =>
            HashCode.Combine(_degree, _embedded, _path, _content?.Length ?? -1, GravitationalParameter, ReferenceRadius,
                _centralTerm);

        private StreamReader OpenSource()
        {
            if (_embedded)
            {
                var stream = typeof(GeopotentialModelParameters).Assembly.GetManifestResourceStream(Egm2008Resource) ??
                             throw new InvalidOperationException(
                                 $"The embedded EGM2008 coefficients ({Egm2008Resource}) are missing from the library.");
                return new StreamReader(stream);
            }

            return _content != null
                ? new StreamReader(new MemoryStream(_content, false))
                : new StreamReader(new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read));
        }

        private static string FullPathOf(string path)
        {
            ArgumentNullException.ThrowIfNull(path, "geopotentialModelPath");
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"The geopotential model file '{path}' does not exist.", fullPath);
            }

            return fullPath;
        }

        private static byte[] ContentOf(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream, "geopotentialModelStream");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }

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
