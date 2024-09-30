using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.OrbitalParameters
{
    /// <summary>
    /// Represents the orientation state of an object in space, including its rotation, angular velocity, epoch, and reference frame.
    /// </summary>
    /// <param name="Rotation">The rotation quaternion representing the orientation.</param>
    /// <param name="AngularVelocity">The angular velocity vector.</param>
    /// <param name="Epoch">The time at which the state is defined.</param>
    /// <param name="ReferenceFrame">The reference frame from which the rotation is applied.</param>
    public record StateOrientation(Quaternion Rotation, Vector3 AngularVelocity, in Time Epoch, Frame ReferenceFrame)
    {
        /// <summary>
        /// Gets the reference frame from which the rotation is applied.
        /// </summary>
        public Frame ReferenceFrame { get; } = ReferenceFrame;

        /// <summary>
        /// Converts the current state orientation to the International Celestial Reference Frame (ICRF).
        /// </summary>
        /// <returns>A new <see cref="StateOrientation"/> instance relative to the ICRF.</returns>
        public StateOrientation RelativeToICRF()
        {
            return RelativeTo(Frame.ICRF);
        }

        /// <summary>
        /// Converts the current state orientation to a specified reference frame.
        /// </summary>
        /// <param name="frame">The target reference frame.</param>
        /// <returns>A new <see cref="StateOrientation"/> instance relative to the specified frame.</returns>
        public StateOrientation RelativeTo(Frame frame)
        {
            if (ReferenceFrame == frame)
            {
                return this;
            }

            var transform = ReferenceFrame.ToFrame(frame, Epoch);
            return new StateOrientation(Rotation * transform.Rotation, transform.AngularVelocity - AngularVelocity, Epoch, frame);
        }

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