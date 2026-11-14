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
    /// Angular velocity associated with <paramref name="Rotation"/>, in rad/s. The library returns it in the SPICE
    /// convention (<c>xf2rav_c</c>): the angular velocity of the destination frame relative to the source frame, expressed
    /// in the source frame. So do <see cref="Frame.ToFrame(Frame, Time)"/>,
    /// <see cref="Frame.GetStateOrientationToICRF(Time)"/> (the frame of a spacecraft included: ICRF relative to the
    /// spacecraft, in the spacecraft frame, managed or kernel-backed), <c>Spacecraft.GetOrientation</c> and
    /// <see cref="SpiceAPI.ReadOrientation"/> (the spacecraft relative to the reference frame, in the reference frame, for
    /// the C-matrix of a CK). <see cref="AtDate"/> takes the other convention.
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
        /// The incremental rotation is left-multiplied: <see cref="AngularVelocity"/> is taken as the angular velocity of
        /// the source frame relative to the destination frame, expressed in the destination frame, the opposite of the
        /// convention in which the library returns it (−R ω, R the rotation and ω the angular velocity of
        /// <see cref="Frame.GetStateOrientationToICRF(Time)"/>). It therefore does not extrapolate a frame transform nor the
        /// attitude of a spacecraft as the library returns them; the attitude record of a spacecraft does not use it.
        /// </remarks>
        public StateOrientation AtDate(in Time date)
        {
            var deltaT = (date - Epoch).TotalSeconds;
            return new StateOrientation(
                (new Quaternion(AngularVelocity.NormalizeOrZero(), AngularVelocity.Magnitude() * deltaT).NormalizeOrZero() * Rotation.NormalizeOrZero()).NormalizeOrZero(), AngularVelocity, date,
                ReferenceFrame);
        }

        /// <summary>
        /// Interpolates between two state orientations: the exact spherical linear interpolation of the rotations (about the
        /// axis of the rotation from this one to <paramref name="other"/>, at the fraction of the angle that
        /// <paramref name="date"/> is of the interval, with no linear fallback for close rotations), and the linear
        /// interpolation of the angular velocities.
        /// </summary>
        /// <param name="other">The later state orientation, in the same reference frame.</param>
        /// <param name="date">The date, between the epochs of the two.</param>
        /// <returns>The state orientation at <paramref name="date"/>.</returns>
        /// <exception cref="ArgumentException">The reference frames differ, or the date is outside the interval.</exception>
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

            // Quaternion.SLERP switches to a normalised Lerp below about 0.028 rad between the rotations, 6e-8 rad off the
            // SLERP at 0.025 rad (#548)
            return new StateOrientation(Quaternion.SlerpByAxisAngle(Rotation, other.Rotation, ratio).Rotation,
                AngularVelocity.LinearInterpolation(other.AngularVelocity, ratio),
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
