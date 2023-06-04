
/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef StateOrientation_H
#define StateOrientation_H

#include <Quaternion.h>
#include <Frames.h>

namespace IO::Astrodynamics::OrbitalParameters
{
	class StateOrientation final
	{
	private:
		const IO::Astrodynamics::Math::Quaternion m_quaternion{};
		const IO::Astrodynamics::Math::Vector3D m_angularVelocity{};
		const IO::Astrodynamics::Time::TDB m_epoch;
		const IO::Astrodynamics::Frames::Frames m_frame;
		
		

	public:
		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param axis 
		 * @param angle 
		 * @param angularVelocity 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(const IO::Astrodynamics::Math::Vector3D &axis, double angle, const IO::Astrodynamics::Math::Vector3D &angularVelocity, IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param q0 
		 * @param q1 
		 * @param q2 
		 * @param q3 
		 * @param v0 
		 * @param v1 
		 * @param v2 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(double q0, double q1, double q2, double q3, double v0, double v1, double v2, IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param quaternion 
		 * @param angularVelocity 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(const IO::Astrodynamics::Math::Quaternion &quaternion, const IO::Astrodynamics::Math::Vector3D &angularVelocity, IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame);

		/**
		 * @brief Assignment operator
		 * 
		 * @param rhs 
		 * @return StateOrientation& 
		 */
		StateOrientation &operator=(const StateOrientation &rhs);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param stateOrientation 
		 */
		StateOrientation(const StateOrientation& stateOrientation);

		/**
		 * @brief Get the Epoch
		 * 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] IO::Astrodynamics::Time::TDB GetEpoch() const;

		/**
		 * @brief Get the Quaternion
		 * 
		 * @return IO::Astrodynamics::Math::Quaternion
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Quaternion GetQuaternion() const;

		/**
		 * @brief Get the Angular Velocity
		 * 
		 * @return IO::Astrodynamics::Math::Vector3D
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Vector3D GetAngularVelocity() const;

		/**
		 * @brief Get the Frame
		 * 
		 * @return IO::Astrodynamics::Frames::Frames
		 */
		[[nodiscard]] IO::Astrodynamics::Frames::Frames GetFrame() const;
	};
}
#endif // !StateOrientation_H
