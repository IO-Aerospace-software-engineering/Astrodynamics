using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters
{
    public class StateVector : OrbitalParameters, IEquatable<StateVector>
    {
        private StateVector _inverse;

        protected override void ResetCache()
        {
            base.ResetCache();
            _inverse = null;
        }

        public Vector3 Position { get; private set; }

        public Vector3 Velocity { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="position"></param>
        /// <param name="velocity"></param>
        /// <param name="observer"></param>
        /// <param name="epoch"></param>
        /// <param name="frame"></param>
        public StateVector(in Vector3 position, in Vector3 velocity, ILocalizable observer, in Time epoch, Frame frame) : base(observer, epoch, frame)
        {
            Position = position;
            Velocity = velocity;
        }

        public override StateVector ToStateVector()
        {
            return this;
        }

        public override KeplerianElements ToKeplerianElements()
        {
            return API.Instance.ConvertStateVectorToConicOrbitalElement(this);
        }

        public static StateVector operator +(StateVector sv1, StateVector sv2)
        {
            if (sv1.Epoch != sv2.Epoch || sv1.Frame != sv2.Frame)
            {
                throw new ArgumentException("State vector must have the same frame and the same epoch");
            }

            return new StateVector(sv1.Position + sv2.Position, sv1.Velocity + sv2.Velocity, sv2.Observer, sv1.Epoch, sv2.Frame);
        }

        public static StateVector operator -(StateVector sv1, StateVector sv2)
        {
            if (sv1.Epoch != sv2.Epoch || sv1.Frame != sv2.Frame)
            {
                throw new ArgumentException("State vector must have the same frame and the same epoch");
            }

            return new StateVector(sv1.Position - sv2.Position, sv1.Velocity - sv2.Velocity, sv2.Observer, sv1.Epoch, sv2.Frame);
        }

        internal void UpdatePosition(in Vector3 position)
        {
            ResetCache();
            Position = position;
        }

        internal void UpdateVelocity(in Vector3 velocity)
        {
            ResetCache();
            Velocity = velocity;
        }


        public double[] ToArray()
        {
            return [Position.X, Position.Y, Position.Z, Velocity.X, Velocity.Y, Velocity.Z];
        }

        public StateVector Inverse()
        {
            if (_inverse is not null)
            {
                return _inverse;
            }

            _inverse ??= new StateVector(Position.Inverse(), Velocity.Inverse(), Observer, Epoch, Frame);
            return _inverse;
        }


        public bool Equals(StateVector other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Position.Equals(other.Position) && Velocity.Equals(other.Velocity) && Observer.NaifId == other.Observer.NaifId && Epoch == other.Epoch && Frame == other.Frame;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StateVector)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Position, Velocity);
        }

        public static bool operator ==(StateVector left, StateVector right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StateVector left, StateVector right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"Epoch : {Epoch.ToString()} Position : {Position} Velocity : {Velocity} Frame : {Frame.Name}";
        }
    }
}