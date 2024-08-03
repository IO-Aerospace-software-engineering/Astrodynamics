using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;

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
        public IEnumerable<Time.Window> FindWindowsInFieldOfViewConstraint(Time.Window searchWindow, Spacecraft observer, INaifObject target,
            Frame targetFrame, ShapeType targetShape, Aberration aberration, TimeSpan stepSize)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (targetFrame == null) throw new ArgumentNullException(nameof(targetFrame));
            return API.Instance.FindWindowsInFieldOfViewConstraint(searchWindow, observer, this, target, targetFrame, targetShape, aberration, stepSize);
        }

        public Vector3 GetBoresightInSpacecraftFrame()
        {
            Quaternion q = Orientation == Vector3.Zero ? Quaternion.Zero : new Quaternion(Orientation.Normalize(), Orientation.Magnitude());
            return Boresight.Rotate(q);
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

        public abstract Task WriteKernelAsync(FileInfo outputFile);

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
    }
}