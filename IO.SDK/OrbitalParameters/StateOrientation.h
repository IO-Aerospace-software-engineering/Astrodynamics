/**
 * @file StateOrientation.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef StateOrientation_H
#define StateOrientation_H

#include <Quaternion.h>
#include <Vector3D.h>
#include <TDB.h>
#include <Frames.h>

namespace IO::SDK::OrbitalParameters
{
	class StateOrientation
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
		StateOrientation(const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param axis 
		 * @param angle 
		 * @param angularVelocity 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(const IO::SDK::Math::Vector3D &axis, const double angle, const IO::SDK::Math::Vector3D &angularVelocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

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
		StateOrientation(const double q0, const double q1, const double q2, const double q3, const double v0, const double v1, const double v2, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

		/**
		 * @brief Construct a new State Orientation object
		 * 
		 * @param quaternion 
		 * @param angularVelocity 
		 * @param epoch 
		 * @param frame 
		 */
		StateOrientation(const IO::SDK::Math::Quaternion &quaternion, const IO::SDK::Math::Vector3D &angularVelocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame);

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
		IO::SDK::Time::TDB GetEpoch() const;

		/**
		 * @brief Get the Quaternion
		 * 
		 * @return IO::SDK::Math::Quaternion 
		 */
		IO::SDK::Math::Quaternion GetQuaternion() const;

		/**
		 * @brief Get the Angular Velocity
		 * 
		 * @return IO::SDK::Math::Vector3D 
		 */
		IO::SDK::Math::Vector3D GetAngularVelocity() const;

		/**
		 * @brief Get the Frame
		 * 
		 * @return IO::SDK::Frames::Frames 
		 */
		IO::SDK::Frames::Frames GetFrame() const;
	};
}
#endif // !StateOrientation_H
