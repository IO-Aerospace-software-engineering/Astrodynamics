/**
 * @file StateOrientation.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "StateOrientation.h"

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame) : m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const IO::SDK::Math::Vector3D &axis, const double angle, const IO::SDK::Math::Vector3D &angularVelocity, IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame) : m_quaternion{axis, angle}, m_angularVelocity{angularVelocity}, m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const double q0, const double q1, const double q2, const double q3, const double v0, const double v1, const double v2, IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame) : m_quaternion{q0, q1, q2, q3}, m_angularVelocity{v0, v1, v2}, m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const IO::SDK::Math::Quaternion &quaternion, const IO::SDK::Math::Vector3D &angularVelocity, IO::SDK::Time::TDB epoch, IO::SDK::Frames::Frames frame) : m_quaternion{quaternion}, m_angularVelocity{angularVelocity}, m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const IO::SDK::OrbitalParameters::StateOrientation &stateOrientation)
= default;

IO::SDK::OrbitalParameters::StateOrientation &IO::SDK::OrbitalParameters::StateOrientation::operator=(const StateOrientation &rhs)
{
	if (this != &rhs)
	{
		const_cast<IO::SDK::Math::Vector3D &>(m_angularVelocity) = rhs.m_angularVelocity;
		const_cast<IO::SDK::Time::TDB &>(m_epoch) = rhs.m_epoch;
		const_cast<IO::SDK::Frames::Frames &>(m_frame) = rhs.m_frame;
		const_cast<IO::SDK::Math::Quaternion &>(m_quaternion) = rhs.m_quaternion;
	}
	return *this;
}

IO::SDK::Time::TDB IO::SDK::OrbitalParameters::StateOrientation::GetEpoch() const
{
	return m_epoch;
}

IO::SDK::Math::Quaternion IO::SDK::OrbitalParameters::StateOrientation::GetQuaternion() const
{
	return m_quaternion;
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::StateOrientation::GetAngularVelocity() const
{
	return m_angularVelocity;
}

IO::SDK::Frames::Frames IO::SDK::OrbitalParameters::StateOrientation::GetFrame() const
{
	return m_frame;
}
