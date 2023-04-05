/**
 * @file StateOrientation.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef StateOrientation_H
#define StateOrientation_H

#include <Quaternion.h>
#include <Frames.h>

namespace IO::SDK::OrbitalParameters
{
	class StateOrientation final
	{
	private:
		const IO::SDK::Math::Quaternion m_quaternion{};
		const IO::SDK::Math::Vector3D m_angularVelocity{};
		const IO::SDK::Time::TDB m_epoch;
		const IO::SDK::Frames::Frames m_frame;
		
		

	public:
		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param axis 
		 * @param angle 
		 * @param angularVelocity 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(const IO::SDK::Math::Vector3D &axis, double angle, const IO::SDK::Math::Vector3D &angularVelocity, IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame);

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
		StateOrientation(double q0, double q1, double q2, double q3, double v0, double v1, double v2, IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param quaternion 
		 * @param angularVelocity 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(const IO::SDK::Math::Quaternion &quaternion, const IO::SDK::Math::Vector3D &angularVelocity, IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame);

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
		 * @return IO::SDK::Time::TDB 
		 */
		[[nodiscard]] IO::SDK::Time::TDB GetEpoch() const;

		/**
		 * @brief Get the Quaternion
		 * 
		 * @return IO::SDK::Math::Quaternion 
		 */
		[[nodiscard]] IO::SDK::Math::Quaternion GetQuaternion() const;

		/**
		 * @brief Get the Angular Velocity
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		[[nodiscard]] IO::SDK::Math::Vector3D GetAngularVelocity() const;

		/**
		 * @brief Get the Frame
		 * 
		 * @return IO::SDK::Frames::Frames 
		 */
		[[nodiscard]] IO::SDK::Frames::Frames GetFrame() const;
	};
}
#endif // !StateOrientation_H
