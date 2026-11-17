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
        /// The half-angle of the field of view in the plane of the boresight and the reference vector, rad: the
        /// <c>FOV_REF_ANGLE</c> of the instrument kernel, the half-angle of a circle
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
        /// The orientation of the instrument relative to the spacecraft: the Euler angles (x, y, z), in radians, about the
        /// axes 1, 2, 3, as the frame kernel the library writes defines them (<c>TKFRAME_..._ANGLES = (x, y, z)</c>,
        /// <c>AXES = (1, 2, 3)</c>, <c>UNITS = 'RADIANS'</c>, relative to the spacecraft frame).
        /// </summary>
        /// <remarks>
        /// NAIF's Frames Required Reading, "Defining a TK Frame Using Euler Angles" (https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/req/frames.html):
        /// "If M is the matrix that converts vectors relative to the TK frame to the RELATIVE frame by left
        /// multiplication, then the angles and axes must satisfy the following relationship: M = [angle_1]axis_1
        /// [angle_2]axis_2 [angle_3]axis_3 where the symbol [ A ]i stands for a rotation by the angle A about the i'th
        /// axis" (the axes are subscripts in the source; its matrices of [A]i are the rotations of the frame, the transposes
        /// of the rotations of a vector by A). Here M = [x]1 [y]2 [z]3 = <c>eul2m(x, y, z, 1, 2, 3)</c> =
        /// <c>pxform(instrument, spacecraft)</c>: a vector in the instrument frame is rotated by -z about Z, then by -y
        /// about Y, then by -x about X, into the spacecraft frame (<see cref="RotationToSpacecraft"/>, #524).
        /// <para>
        /// The angles are finite, checked when the instrument is built (#715). Any finite angle is accepted; the frame
        /// SPICE reads from the kernel the library writes agrees with <see cref="RotationToSpacecraft"/> to a few ulps of
        /// each angle, which SPICE's text kernel reader and its unit conversion (<c>CONVRT</c>) can move by a few ulps:
        /// 1.8e-15 rad within [−2π, 2π], growing with the angle (1.6e-2 rad near 1e13, 0.268 rad at 1e300; measured with
        /// CSPICE N0067, #715; every finite angle accepted, decided by S. Guillet, 2026-11-17). Give angles within a few turns.
        /// </para>
        /// </remarks>
        public Vector3 Orientation { get; }

        public Spacecraft Spacecraft { get; }

        /// <summary>
        /// Instrument constructor
        /// </summary>
        /// <remarks>
        /// The field of view is checked as CSPICE's <c>getfov</c> reads it from the instrument kernel the library writes
        /// (#707): a boresight with finite components and of non-zero magnitude (SPICE(ZEROBORESIGHT)), a reference vector
        /// likewise and not parallel to the boresight (parallel: |b × r| = 0 on the given vectors, <c>getfov</c>'s test,
        /// SPICE(BADREFVECTORSPEC)), and a finite positive half-angle. Before 11.0.0 the vectors were not checked and a NaN
        /// or infinite half-angle was accepted: the instrument was built, its getters returned zero or NaN vectors,
        /// <c>InstrumentPointingToAttitude</c> gave the identity or a NaN attitude, its kernel was one SPICE rejects or cannot
        /// read, and the field-of-view search threw late. The upper bound of the half-angles, π/2 − 1e-6 for <c>gftfov</c>,
        /// is checked by the search. The Euler angles of the orientation are finite (#715): before 11.0.0 a NaN or an
        /// infinite angle gave a NaN <see cref="RotationToSpacecraft"/>, NaN spacecraft-frame getters, and a frame kernel
        /// holding <c>ANGLES = ( NaN,0,0 )</c>, which SPICE fails to load (SPICE(NUMBEREXPECTED)).
        /// </remarks>
        /// <param name="naifId"></param>
        /// <param name="name"></param>
        /// <param name="model"></param>
        /// <param name="fieldOfView">The half-angle in the plane of the boresight and the reference vector, rad: finite and
        /// positive</param>
        /// <param name="shape"></param>
        /// <param name="boresight">The boresight: finite components, a non-zero magnitude</param>
        /// <param name="refVector">The reference vector: finite components, a non-zero magnitude, not parallel to the
        /// boresight</param>
        /// <param name="orientation">The Euler angles (x, y, z) of <see cref="Orientation"/>, rad: finite</param>
        /// <param name="spacecraft"></param>
        /// <exception cref="ArgumentException">The name or the model is empty; or, naming the parameter, the field of view is
        /// not a finite positive number, the boresight or the reference vector has a component that is not finite or a
        /// magnitude of 0 (the boresight checked first), or the reference vector is parallel to the boresight (#707), or an
        /// Euler angle of the orientation is not finite (#715).</exception>
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

            // A NaN passed the former test fieldOfView <= 0, and an infinity was not checked (#707)
            if (!double.IsFinite(fieldOfView) || fieldOfView <= 0.0)
            {
                throw new ArgumentException("fieldOfView must be a finite positive number", nameof(fieldOfView));
            }

            // getfov's rules on the vectors of the instrument kernel (#707), with the finite rule of the body axes of a
            // spacecraft and the body vectors of a TRIAD (#703): SPICE reads no NaN nor infinity in a kernel
            Vector3.ThrowIfNoDirection(boresight, "An instrument boresight", nameof(boresight));
            Vector3.ThrowIfNoDirection(refVector, "An instrument reference vector", nameof(refVector));
            if (boresight.Cross(refVector).Magnitude() == 0.0)
            {
                throw new ArgumentException(
                    "An instrument reference vector must not be parallel to its boresight: they define the plane of the field of view (getfov, SPICE(BADREFVECTORSPEC)).",
                    nameof(refVector));
            }

            // SPICE reads no NaN nor infinity in the ANGLES of the frame kernel (SPICE(NUMBEREXPECTED)), and the rotation of such
            // an angle is NaN (#715)
            if (!double.IsFinite(orientation.X) || !double.IsFinite(orientation.Y) || !double.IsFinite(orientation.Z))
            {
                throw new ArgumentException(
                    $"The Euler angles of an instrument orientation must be finite: {orientation} has no rotation, and SPICE cannot read it in the frame kernel.",
                    nameof(orientation));
            }

            if (naifId >= 0) throw new ArgumentOutOfRangeException(nameof(naifId));

            Name = name;
            Model = model;
            FieldOfView = fieldOfView;
            Shape = shape;
            Boresight = boresight;
            RefVector = refVector;
            Orientation = orientation;
            RotationToSpacecraft = EulerToSpacecraft(orientation);
            Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
            // The instrument FK defines a TK frame of id naifId relative to the spacecraft frame, of id NaifId × 1000: the
            // same id would define the spacecraft frame relative to itself, SPICE(BADFRAMESPEC2) (#676)
            if (naifId == spacecraft.Frame.Id)
            {
                throw new ArgumentOutOfRangeException(nameof(naifId), naifId,
                    $"The instrument id {naifId} is the id of the frame of {spacecraft.Name}, {spacecraft.Frame.Id} (NaifId × 1000): the instrument frame would be defined relative to itself. Take an id in [{spacecraft.Frame.Id - 999}, {spacecraft.Frame.Id - 1}].");
            }

            NaifId = naifId;
        }

        /// <summary>
        /// The half-angle across the reference plane of an ellipse or a rectangle, as given: a finite positive number, or
        /// <see cref="ArgumentOutOfRangeException"/> naming <paramref name="crossAngle"/>, one message for every value
        /// rejected. SPICE reads no NaN nor infinity in a kernel (#707: +∞ and a NaN of positive sign passed
        /// <c>ThrowIfNegative</c>, and the instrument kernel held <c>FOV_CROSS_ANGLE = Infinity</c>, SPICE(NUMBEREXPECTED) on
        /// loading). A cross angle of 0 (#715; −0 was rejected by <c>ThrowIfNegative</c> with another message, +0 accepted)
        /// is a field of no width that the library's searches rejected when called and that CSPICE's <c>gftfov</c> rejects
        /// (SPICE(DEGENERATECASE): the rectangle's consecutive boundary vectors are not independent, <c>zzfovaxi</c>; the
        /// ellipse's semi-axis has a zero length, <c>zzgffvu</c>) or, for an ellipse whose computed semi-axis rounds to a
        /// non-zero length, searches as a segment, as the field of view of 0 the constructor rejects.
        /// </summary>
        private protected static double CheckCrossAngle(double crossAngle)
        {
            // -0 <= 0: one message for +0, -0 and a negative angle (#715)
            if (!double.IsFinite(crossAngle) || crossAngle <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(crossAngle), crossAngle,
                    "The cross angle of an instrument must be a finite positive number: SPICE reads no NaN nor infinity in the instrument kernel, and a field of view of zero width is degenerate for gftfov.");
            }

            return crossAngle;
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
        /// except the light-time corrected epoch of the target, up to its light time before the start (reception) or after
        /// the end (transmission, XLT and XCN), which for a propagated spacecraft target is extrapolated; a window holding
        /// at a bound of the search starts or ends there.
        /// </para>
        /// <para>
        /// <b>Corrections.</b> The light time of CSPICE's <c>spkltc</c> (LT, CN and their transmission forms) and, for "+S",
        /// the stellar aberration of <c>stelab</c> (<c>stlabx</c> for transmission), as <c>gftfov</c> applies them. A
        /// <see cref="Star"/> is a direction: no light time, the stellar aberration applied, as <c>gfrfov</c> reads a ray.
        /// </para>
        /// <para>
        /// <b>Step.</b> <c>gftfov</c> finds every window and gap longer than the step. The managed search finds every window
        /// and gap shorter than the step when the step is at most half the time between the extremes of the margin, down to
        /// the resolution of Brent's <c>localmin</c>: one narrower than about 2 √ε h + 2τ/3 (h the step, τ the 1 µs
        /// tolerance; 2.5 µs at 1 min, 19 µs at 10 min) can be missed, and a tangency between samples is not found (#668);
        /// with a step longer than an event, the two paths may then differ, the managed one finding the event.
        /// </para>
        /// <para>
        /// <b>Target frame.</b> Used as given for an ellipsoid, without a check of its center; <c>gftfov</c> requires a
        /// body-fixed frame centered on the target. Ignored for a point, as <c>gftfov</c> ignores it.
        /// </para>
        /// <para>
        /// <b>Tiny half-angles.</b> The half-angles are finite and positive (#707, #715), but a field of a half-angle below
        /// about 1e-16 rad can be degenerate for <c>gftfov</c>, whose tests of zero lengths and zero cross products are exact
        /// on computed vectors, so their threshold depends on the boresight: SPICE(DEGENERATECASE) up to 1.53e-17 rad for a
        /// rectangle and 1.42e-17 rad for an ellipse with the boresight at the Moon, none down to 5e-324 with the boresight
        /// +Z but for an ellipse and an ellipsoid target, SPICE(BADAXISLENGTH) up to 2.73e-319 (measured with
        /// <c>fovtrg_c</c>, CSPICE N0067, #715). The managed path searches such fields, so the two paths can then disagree (decided by S. Guillet, 2026-11-17);
        /// its own construction of a circle or an ellipse loses its precision far below (the Moon's ellipsoid at the
        /// boresight not found for a half-angle of 1e-155 rad in the reference plane, measured), and it throws
        /// <see cref="InvalidOperationException"/> where the squares of the semi-axes of the cone of the field underflow
        /// (below about 6e-171 rad at the Moon's distance).
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
        /// <exception cref="InvalidOperationException">The field of view cannot be searched: an angular radius above
        /// π/2 − 1e-6, or a shape other than a
        /// circle on an instrument that is neither an <see cref="EllipticalInstrument"/> nor a <see cref="RectangularInstrument"/>;
        /// or, on the managed path, a circle or an ellipse whose half-angles are so small that the squares of the semi-axes of
        /// the cone of the field underflow at the target's distance, or a point target at the observer (distance 0: it has no
        /// direction) (#715; before 11.0.0 an unrelated <see cref="ArgumentException"/> for both), or the instrument's spacecraft is managed and has no attitude (its attitude record is empty:
        /// never propagated and no orientation added with <see cref="SpacecraftFrame.AddStateOrientationToICRF"/>, built from
        /// a TLE, or propagated by the TLE propagator; before 11.0.0 the identity of ICRF was searched, #682), or the observer
        /// is inside the ellipsoid of the target (at its center included, an unrelated <see cref="ArgumentException"/> before
        /// 11.0.0 for a circle or an ellipse); or, on the path of CSPICE's <c>gftfov_c</c>, SPICE cannot run the search (no
        /// instrument kernel defines the field of view, a body or an epoch without ephemeris data, an epoch outside the coverage
        /// of the frame data or of the C-kernel), with SPICE's message (fourth round of #668; before 11.0.0 the search returned
        /// no window).</exception>
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

        /// <summary>
        /// The rotation from the instrument frame to the spacecraft frame, <c>vectorInSpacecraft =
        /// vectorInInstrument.Rotate(q)</c>, of the Euler angles <see cref="Orientation"/> (x, y, z) about the axes 1, 2, 3,
        /// as SPICE defines the frame kernel the library writes (<c>TKFRAME_..._ANGLES</c>, <c>AXES = (1, 2, 3)</c>;
        /// Frames Required Reading, "Defining a TK Frame Using Euler Angles", https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/req/frames.html):
        /// <c>pxform(instrument, spacecraft) = eul2m(x, y, z, 1, 2, 3)</c> = [x]1 [y]2 [z]3, the vector rotated by -z about
        /// Z, then -y about Y, then -x about X (#524).
        /// </summary>
        public Quaternion RotationToSpacecraft { get; }

        public Vector3 GetBoresightInSpacecraftFrame()
        {
            return Boresight.Rotate(RotationToSpacecraft);
        }

        public Vector3 GetRefVectorInSpacecraftFrame()
        {
            return RefVector.Rotate(RotationToSpacecraft);
        }

        // The rotation of RotationToSpacecraft
        internal static Quaternion EulerToSpacecraft(in Vector3 orientation)
        {
            if (orientation == Vector3.Zero)
            {
                return Quaternion.Identity;
            }

            return (Quaternion.FromAxisAngle(Vector3.VectorX, -orientation.X) * Quaternion.FromAxisAngle(Vector3.VectorY, -orientation.Y) * Quaternion.FromAxisAngle(Vector3.VectorZ, -orientation.Z)).NormalizeOrZero();
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
            return Spacecraft.Frame.GetStateOrientationToICRF(date).Rotation * RotationToSpacecraft;
        }

        /// <summary>
        /// Writes the frame kernel of the instrument, a TK frame relative to the spacecraft frame, whose kernel variable
        /// <c>FRAME_&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;</c> holds the upper-cased names.
        /// </summary>
        /// <param name="outputFile">The frame kernel to write.</param>
        /// <exception cref="InvalidOperationException">The names make a kernel variable name SPICE cannot read: a blank, a
        /// tab or another whitespace or control character, a quote, a parenthesis, a comma or '=', or more than 32 bytes in
        /// UTF-8, so 25 ASCII characters at most for the two names (Kernel Required Reading, "Variable Name Rules", #676);
        /// nothing is written.</exception>
        public async Task WriteFrameAsync(FileInfo outputFile)
        {
            CheckFrameKernelName("frame kernel");
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
        /// <see cref="FindWindowsInFieldOfViewConstraint"/>), or the spacecraft is managed and has no attitude (#682; before
        /// 11.0.0 the identity of ICRF was used).</exception>
        public virtual bool IsInFOV(Time date, ILocalizable target, Aberration aberration)
        {
            return new FieldOfViewEvaluator(this, target, ShapeType.Point, null, aberration).Margin(date) >= 0.0;
        }

        /// <summary>
        /// Writes the instrument kernel, whose <c>INS&lt;id&gt;_FOV_FRAME</c> names the frame of the instrument frame kernel,
        /// <c>&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;</c> upper-cased.
        /// </summary>
        /// <param name="outputFile">The instrument kernel to write.</param>
        /// <exception cref="InvalidOperationException">The names make a frame the instrument frame kernel cannot define, by
        /// the rule of <see cref="WriteFrameAsync"/> (#676): a quote would end the string value, which SPICE then rejects
        /// (SPICE(TYPEMISMATCH) at <c>furnsh_c</c>); nothing is written.</exception>
        public abstract Task WriteKernelAsync(FileInfo outputFile);

        /// <summary>
        /// Checks the kernel variable <c>FRAME_&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;</c> of the instrument frame kernel, whose
        /// frame name the instrument kernel names too, before a writer opens its file (#676).
        /// </summary>
        /// <param name="kernel">The kernel the writer writes, for the message.</param>
        /// <exception cref="InvalidOperationException">SPICE cannot read the variable.</exception>
        private protected void CheckFrameKernelName(string kernel)
        {
            Kernels.TextKernelNames.Check("FRAME_" + Spacecraft.Name.ToUpper() + "_" + Name.ToUpper(),
                $"the instrument '{Name}' of the spacecraft '{Spacecraft.Name}'", kernel);
        }

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