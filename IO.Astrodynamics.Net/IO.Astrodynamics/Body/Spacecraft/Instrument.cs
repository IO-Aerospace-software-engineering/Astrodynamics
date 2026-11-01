using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft
{
    public abstract class Instrument : INaifObject, IEquatable<Instrument>
    {
        /// <summary>
        /// Naif identifier
        /// </summary>
        public int NaifId { get; }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Model name
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Field of view
        /// </summary>
        public double FieldOfView { get; }


        /// <summary>
        /// Shape
        /// </summary>
        public InstrumentShape Shape { get; }

        /// <summary>
        /// Boresight vector
        /// </summary>
        public Vector3 Boresight { get; }

        /// <summary>
        /// Ref vector in the boresight plane
        /// </summary>
        public Vector3 RefVector { get; }

        /// <summary>
        /// Instrument rotation relative to instrument platform. The orientation is expressed in euler angles
        /// </summary>
        public Vector3 Orientation { get; }

        public Spacecraft Spacecraft { get; }

        /// <summary>
        /// Instrument constructor
        /// </summary>
        /// <param name="naifId"></param>
        /// <param name="name"></param>
        /// <param name="model"></param>
        /// <param name="fieldOfView"></param>
        /// <param name="shape"></param>
        /// <param name="boresight"></param>
        /// <param name="refVector"></param>
        /// <param name="orientation"></param>
        /// <param name="spacecraft"></param>
        /// <exception cref="ArgumentException"></exception>
        protected Instrument(Spacecraft spacecraft, int naifId, string name, string model, double fieldOfView, InstrumentShape shape, Vector3 boresight, Vector3 refVector,
            Vector3 orientation)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Instrument requires a name");
            }

            if (string.IsNullOrEmpty(model))
            {
                throw new ArgumentException("Instrument requires a model");
            }

            if (fieldOfView <= 0)
            {
                throw new ArgumentException("fieldOfView must be a positive number");
            }

            if (naifId >= 0) throw new ArgumentOutOfRangeException(nameof(naifId));

            Name = name;
            Model = model;
            FieldOfView = fieldOfView;
            Shape = shape;
            Boresight = boresight;
            RefVector = refVector;
            Orientation = orientation;
            Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
            NaifId = naifId;
        }

        /// <summary>
        /// The windows, in TDB, where the target is in the field of view of the instrument, its spacecraft the observer:
        /// the windows of CSPICE's <c>gftfov</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The condition.</b> A <see cref="ShapeType.Point"/> target is in view when the direction of its center is within
        /// the field; an <see cref="ShapeType.Ellipsoid"/> target when its apparent disc, the ellipsoid of the radii of a
        /// <see cref="CelestialBody"/> oriented by <paramref name="targetFrame"/> at the light-time corrected epoch, overlaps
        /// the field, partly or whole. The field is that of the instrument kernel: <see cref="FieldOfView"/> is the half-angle
        /// in the plane of the boresight and the reference vector (<c>FOV_REF_ANGLE</c>), the cross angle of an ellipse or a
        /// rectangle the half-angle across it (<c>FOV_CROSS_ANGLE</c>), the reference vector is made perpendicular to the
        /// boresight, and the axes are read at the epoch through the spacecraft's attitude.
        /// </para>
        /// <para>
        /// <b>The two paths.</b> When the instrument's spacecraft and the target are both in the kernels, the search is
        /// CSPICE's <c>gftfov</c>, which reads the instrument and frame kernels loaded under <see cref="NaifId"/>. Otherwise
        /// it is the managed search of the library, which computes what <c>gftfov</c> computes, without writing a kernel: a
        /// continuous margin of the target in the field, positive in view and zero at the contact (for a circle or an ellipse,
        /// <c>gftfov</c>'s construction, an ellipsoid whose limb is the field's cone, and the overlap margin of the occultation
        /// search, with the target mapped to the unit sphere, measured for targets of aspect ratio up to 1.0034 and fields of
        /// aspect ratio up to 10 311; for a rectangle, the largest discriminant over the rectangle of the ray through the
        /// ellipsoid, in closed form), sampled at the step, its extremes searched between the samples, and its sign changes
        /// located by Brent's method to 1 µs, CSPICE's <c>SPICE_GF_CNVTOL</c>. No state outside the search window is read,
        /// except the light-time corrected epoch of the target; a window holding at a bound of the search starts or ends there.
        /// </para>
        /// <para>
        /// <b>Corrections.</b> The light time of CSPICE's <c>spkltc</c> (LT, CN and their transmission forms) and, for "+S",
        /// the stellar aberration of <c>stelab</c> (<c>stlabx</c> for transmission), as <c>gftfov</c> applies them. A
        /// <see cref="Star"/> is a direction: no light time, the stellar aberration applied, as <c>gfrfov</c> reads a ray.
        /// </para>
        /// <para>
        /// <b>Step.</b> <c>gftfov</c> finds every window and gap longer than the step. The managed search finds every window
        /// and gap, however short, when the step is at most half the time between the extremes of the margin; with a step
        /// longer than an event, the two paths may then differ, the managed one finding the event.
        /// </para>
        /// <para>
        /// <b>Target frame.</b> Used as given for an ellipsoid, without a check of its center; <c>gftfov</c> requires a
        /// body-fixed frame centered on the target. Ignored for a point, as <c>gftfov</c> ignores it.
        /// </para>
        /// </remarks>
        /// <param name="searchWindow">The search window.</param>
        /// <param name="observer">Not used: the observer is the instrument's spacecraft.</param>
        /// <param name="target">The target.</param>
        /// <param name="targetFrame">The body-fixed frame of the target's ellipsoid; ignored for a point.</param>
        /// <param name="targetShape">The target's shape.</param>
        /// <param name="aberration">The aberration correction.</param>
        /// <param name="stepSize">The step of the search, positive.</param>
        /// <returns>The windows, in TDB.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The step is not positive.</exception>
        /// <exception cref="ArgumentException">The search window ends before it starts, the target is the instrument's
        /// spacecraft or is an ellipsoid without radii (a site, a spacecraft, a barycenter, a Lagrange point, a star), or, on the
        /// managed path, the search window leaves the propagated states of the instrument's spacecraft or of a spacecraft
        /// target, which would be extrapolated.</exception>
        /// <exception cref="InvalidOperationException">The field of view cannot be searched: a boresight zero or parallel to
        /// the reference vector, a half-angle not positive, an angular radius above π/2 − 1e-6, or a shape other than a
        /// circle on an instrument that is neither an <see cref="EllipticalInstrument"/> nor a <see cref="RectangularInstrument"/>;
        /// or, on the managed path, the observer is inside the ellipsoid of the target.</exception>
        public IEnumerable<Window> FindWindowsInFieldOfViewConstraint(Window searchWindow, Spacecraft observer, ILocalizable target,
            Frame targetFrame, ShapeType targetShape, Aberration aberration, TimeSpan stepSize)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));

            if (!(Spacecraft.IsSpiceBacked && target.IsSpiceBacked))
            {
                return FieldOfViewSearch.Managed(this, searchWindow, target, targetShape, targetFrame, aberration, stepSize);
            }

            FieldOfViewSearch.Check(this, searchWindow, target, targetShape, stepSize);
            return SpiceAPI.Instance.FindWindowsInFieldOfViewConstraint(
                searchWindow, Spacecraft.NaifId, NaifId, target.NaifId,
                targetFrame, targetShape, aberration, stepSize);
        }

        public Vector3 GetBoresightInSpacecraftFrame()
        {
            Quaternion q = Orientation == Vector3.Zero ? Quaternion.Zero : new Quaternion(Orientation.Normalize(), Orientation.Magnitude());
            return Boresight.Rotate(q);
        }

        public Vector3 GetRefVectorInSpacecraftFrame()
        {
            Quaternion q = Orientation == Vector3.Zero ? Quaternion.Zero : new Quaternion(Orientation.Normalize(), Orientation.Magnitude());
            return RefVector.Rotate(q);
        }

        public Vector3 GetBoresightInICRFFrame(in Time date)
        {
            return Boresight.Rotate(RotationToICRF(date));
        }

        public Vector3 GetRefVectorInICRFFrame(in Time date)
        {
            return RefVector.Rotate(RotationToICRF(date));
        }

        /// <summary>
        /// The axes of the field of view in ICRF at an epoch, from one read of the spacecraft's attitude: the boresight, the
        /// reference vector made perpendicular to it, and their cross product.
        /// </summary>
        internal FieldOfViewAxes GetFieldOfViewAxesInICRFFrame(in Time date)
        {
            var q = RotationToICRF(date);
            return FieldOfViewGeometry.Axes(Boresight.Rotate(q), RefVector.Rotate(q));
        }

        private Quaternion RotationToICRF(in Time date)
        {
            Quaternion q = Orientation == Vector3.Zero ? Quaternion.Zero : new Quaternion(Orientation.Normalize(), Orientation.Magnitude());
            return Spacecraft.Frame.GetStateOrientationToICRF(date).Rotation * q.Conjugate();
        }

        public async Task WriteFrameAsync(FileInfo outputFile)
        {
            await using var stream = this.GetType().Assembly.GetManifestResourceStream("IO.Astrodynamics.Templates.InstrumentFrameTemplate.tf");
            using StreamReader sr = new StreamReader(stream ?? throw new InvalidOperationException());
            var templateData = await sr.ReadToEndAsync();
            var data = templateData
                .Replace("{spacecraftname}", Spacecraft.Name.ToUpper())
                .Replace("{instrumentname}", Name.ToUpper())
                .Replace("{instrumentid}", NaifId.ToString())
                .Replace("{framename}", Spacecraft.Name.ToUpper() + "_" + Name.ToUpper())
                .Replace("{spacecraftid}", Spacecraft.NaifId.ToString())
                .Replace("{spacecraftframe}", Spacecraft.Frame.Name.ToUpper())
                .Replace("{x}", Orientation.X.ToString(CultureInfo.InvariantCulture))
                .Replace("{y}", Orientation.Y.ToString(CultureInfo.InvariantCulture))
                .Replace("{z}", Orientation.Z.ToString(CultureInfo.InvariantCulture));
            await using var sw = new StreamWriter(outputFile.FullName);
            await sw.WriteAsync(data);
        }

        /// <summary>
        /// Whether the center of the target is in the field of view at an epoch: the condition of CSPICE's <c>fovtrg</c> with
        /// the shape <c>POINT</c>, and the point condition of <see cref="FindWindowsInFieldOfViewConstraint"/>, with the
        /// same field, the same corrections ("+S" included) and a <see cref="Star"/> read as a direction. The search does not
        /// call this method.
        /// </summary>
        /// <param name="date">The epoch.</param>
        /// <param name="target">The target.</param>
        /// <param name="aberration">The aberration correction.</param>
        /// <exception cref="ArgumentException">The target is the instrument's spacecraft.</exception>
        /// <exception cref="InvalidOperationException">The field of view cannot be searched (see
        /// <see cref="FindWindowsInFieldOfViewConstraint"/>).</exception>
        public virtual bool IsInFOV(Time date, ILocalizable target, Aberration aberration)
        {
            return new FieldOfViewEvaluator(this, target, ShapeType.Point, null, aberration).Margin(date) >= 0.0;
        }

        /// <summary>
        /// The azimuth and elevation of the target in the instrument's axes before #515: its reference vector is not made
        /// perpendicular to the boresight, and its corrections are those of <see cref="CelestialItem.GetEphemeris"/>.
        /// </summary>
        [Obsolete("PositionInFOV does not make the reference vector perpendicular to the boresight; use IsInFOV, CSPICE's fovtrg condition. It will be removed in 11.0.")]
        protected (double azimuth, double elevation, bool isInFov) PositionInFOV(Time date, ILocalizable target, Aberration aberration)
        {
            var objectPosition = target.GetEphemeris(date, Spacecraft, Frame.ICRF, aberration).ToStateVector();
            // Calculate the vector from the camera to the object
            // Compute the vector from the camera to the object
            Vector3 toObject = objectPosition.Position;
            
            // Ensure boresight and refVector are normalized
            var boresight = GetBoresightInICRFFrame(date).Normalize();
            double z_cam = toObject * boresight; // Dot product with forward vector

            // Check if the object is in front of the camera
            if (z_cam <= 0)
                return (double.NaN, double.NaN, false);
            
            var refVector = GetRefVectorInICRFFrame(date).Normalize();

            // Compute the right vector (x-axis) of the camera's coordinate system
            Vector3 rightVector = boresight.Cross(refVector).Normalize();

            // Project toObject onto the camera's coordinate axes
            double x_cam = toObject * rightVector; // Dot product with right vector
            double y_cam = toObject * refVector; // Dot product with up vector
           

            // Calculate azimuth (horizontal angle) and elevation (vertical angle)
            double azimuth = System.Math.Atan2(x_cam, z_cam);
            double elevation = System.Math.Atan2(y_cam, z_cam);
            return (azimuth, elevation, true);
        }

        public abstract Task WriteKernelAsync(FileInfo outputFile);

        #region Operators

        public bool Equals(Instrument other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return NaifId == other.NaifId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Instrument)obj);
        }

        public override int GetHashCode()
        {
            return NaifId;
        }

        public static bool operator ==(Instrument left, Instrument right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Instrument left, Instrument right)
        {
            return !Equals(left, right);
        }

        #endregion
    }
}