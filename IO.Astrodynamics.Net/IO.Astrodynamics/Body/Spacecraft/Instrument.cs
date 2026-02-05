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
        private readonly GeometryFinder _geometryFinder = new GeometryFinder();

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
        ///     Find time window when a target is in instrument's field of view
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="observer"></param>
        /// <param name="target"></param>
        /// <param name="targetFrame"></param>
        /// <param name="targetShape"></param>
        /// <param name="aberration"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public IEnumerable<Window> FindWindowsInFieldOfViewConstraint(Window searchWindow, Spacecraft observer, ILocalizable target,
            Frame targetFrame, ShapeType targetShape, Aberration aberration, TimeSpan stepSize)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));

            Func<Time, bool> calculateInFov = date => IsInFOV(date, target, aberration);

            return _geometryFinder.FindWindowsWithCondition(searchWindow, calculateInFov, RelationnalOperator.Equal, true, stepSize);
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
            Quaternion q = Orientation == Vector3.Zero ? Quaternion.Zero : new Quaternion(Orientation.Normalize(), Orientation.Magnitude());
            q = Spacecraft.Frame.GetStateOrientationToICRF(date).Rotation * q.Conjugate();
            return Boresight.Rotate(q);
        }

        public Vector3 GetRefVectorInICRFFrame(in Time date)
        {
            Quaternion q = Orientation == Vector3.Zero ? Quaternion.Zero : new Quaternion(Orientation.Normalize(), Orientation.Magnitude());
            q = Spacecraft.Frame.GetStateOrientationToICRF(date).Rotation * q.Conjugate();
            return RefVector.Rotate(q);
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

        public virtual bool IsInFOV(Time date, ILocalizable target, Aberration aberration)
        {
            var (azimuth, elevation, isInFov) = PositionInFOV(date, target, aberration);
            if (!isInFov) return false;

            // Check if the object is within the horizontal and vertical field of view
            return (System.Math.Abs(azimuth) <= FieldOfView) && (System.Math.Abs(elevation) <= FieldOfView);
        }

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