#include "StateOrientation.h"

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame) : m_epoch{epoch}, m_frame{frame}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const IO::SDK::Math::Vector3D &axis, const double angle, const IO::SDK::Math::Vector3D &angularVelocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame) : m_quaternion{axis, angle}, m_angularVelocity{angularVelocity}, m_epoch{epoch}, m_frame{frame}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const double q0, const double q1, const double q2, const double q3, const double v0, const double v1, const double v2, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame) : m_quaternion{q0, q1, q2, q3}, m_angularVelocity{v0, v1, v2}, m_epoch{epoch}, m_frame{frame}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const IO::SDK::Math::Quaternion &quaternion, const IO::SDK::Math::Vector3D &angularVelocity, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame) : m_quaternion{quaternion}, m_angularVelocity{angularVelocity}, m_epoch{epoch}, m_frame{frame}
{
}

IO::SDK::OrbitalParameters::StateOrientation::StateOrientation(const IO::SDK::OrbitalParameters::StateOrientation &stateOrientation) : m_quaternion{stateOrientation.m_quaternion}, m_angularVelocity{stateOrientation.m_angularVelocity}, m_epoch{stateOrientation.m_epoch}, m_frame{stateOrientation.m_frame}
{
}

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
