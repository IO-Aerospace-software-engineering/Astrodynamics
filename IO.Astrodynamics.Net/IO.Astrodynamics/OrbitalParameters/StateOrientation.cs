using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters
{
    /// <summary>
    /// Represents a time-stamped frame transform together with its angular velocity.
    /// </summary>
    /// <param name="Rotation">
    /// Rotation that maps vectors from <paramref name="ReferenceFrame"/> into the destination frame
    /// implied by the calling context.
    /// For example, <c>frame.GetStateOrientationToICRF(epoch)</c> returns a rotation
    /// <c>frame → ICRF</c>.
    /// </param>
    /// <param name="AngularVelocity">
    /// Angular velocity associated with <paramref name="Rotation"/>.
    /// In this codebase it is used with left-multiplied incremental rotations in <see cref="AtDate"/>,
    /// so it is expressed in the same inertial/destination frame as the returned rotation acts into.
    /// </param>
    /// <param name="Epoch">The time at which the state is defined.</param>
    /// <param name="ReferenceFrame">Source frame of the rotation.</param>
    public record StateOrientation(Quaternion Rotation, Vector3 AngularVelocity, in Time Epoch, Frame ReferenceFrame)
    {
        /// <summary>
        /// Gets the source frame of <see cref="Rotation"/>.
        /// The destination frame depends on the API that produced this instance.
        /// </summary>
        public Frame ReferenceFrame { get; } = ReferenceFrame;

        /// <summary>
        /// Re-expresses the current orientation so that it maps from the same source frame into ICRF.
        /// </summary>
        /// <returns>A new <see cref="StateOrientation"/> instance relative to the ICRF.</returns>
        public StateOrientation RelativeToICRF()
        {
            return RelativeTo(Frame.ICRF);
        }

        /// <summary>
        /// Re-expresses the current orientation in a specified reference frame.
        /// </summary>
        /// <param name="frame">The target reference frame.</param>
        /// <returns>
        /// A new <see cref="StateOrientation"/> whose rotation maps from the same physical source orientation
        /// into <paramref name="frame"/>.
        /// </returns>
        public StateOrientation RelativeTo(Frame frame)
        {
            if (ReferenceFrame == frame)
            {
                return this;
            }

            var transform = ReferenceFrame.ToFrame(frame, Epoch);
            return new StateOrientation(Rotation * transform.Rotation, transform.AngularVelocity - AngularVelocity, Epoch, frame);
        }

        /// <summary>
        /// Propagates the orientation to another epoch using constant angular velocity.
        /// </summary>
        /// <remarks>
        /// The incremental rotation is left-multiplied, so callers providing angular velocities manually
        /// should keep that destination-frame convention in mind.
        /// </remarks>
        public StateOrientation AtDate(in Time date)
        {
            var deltaT = (date - Epoch).TotalSeconds;
            return new StateOrientation(
                (new Quaternion(AngularVelocity.Normalize(), AngularVelocity.Magnitude() * deltaT).Normalize() * Rotation.Normalize()).Normalize(), AngularVelocity, date,
                ReferenceFrame);
        }

        /// <summary>
        /// Interpolates between two state orientations.
        /// </summary>
        /// <param name="other"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public StateOrientation Interpolate(StateOrientation other, Time date)
        {
            if (ReferenceFrame != other.ReferenceFrame)
            {
                throw new ArgumentException("Cannot interpolate between two different reference frames.");
            }
            if(date < Epoch || date > other.Epoch)
            {
                throw new ArgumentException("Date is out of range.");
            }
            var ratio = (date - Epoch).TotalSeconds / (other.Epoch - Epoch).TotalSeconds;

            return new StateOrientation(Rotation.SLERP(other.Rotation, ratio), AngularVelocity.LinearInterpolation(other.AngularVelocity, ratio),
                Epoch + (other.Epoch - Epoch) * ratio, ReferenceFrame);
        }

        /// <summary>
        /// Returns a string representation of the state orientation.
        /// </summary>
        /// <returns>A string that represents the current state orientation.</returns>
        public override string ToString()
        {
            return $"Epoch : {Epoch.ToTDB().ToString()} Orientation : {Rotation} Angular velocity : {AngularVelocity} Frame : {ReferenceFrame.Name}";
        }
    }
}
