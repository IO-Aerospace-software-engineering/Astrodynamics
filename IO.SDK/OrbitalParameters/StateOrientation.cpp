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
