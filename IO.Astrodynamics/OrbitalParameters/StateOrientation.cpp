/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <StateOrientation.h>

IO::Astrodynamics::OrbitalParameters::StateOrientation::StateOrientation(IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame) : m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::Astrodynamics::OrbitalParameters::StateOrientation::StateOrientation(const IO::Astrodynamics::Math::Vector3D &axis, const double angle, const IO::Astrodynamics::Math::Vector3D &angularVelocity, IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame) : m_quaternion{axis, angle}, m_angularVelocity{angularVelocity}, m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::Astrodynamics::OrbitalParameters::StateOrientation::StateOrientation(const double q0, const double q1, const double q2, const double q3, const double v0, const double v1, const double v2, IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame) : m_quaternion{q0, q1, q2, q3}, m_angularVelocity{v0, v1, v2}, m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::Astrodynamics::OrbitalParameters::StateOrientation::StateOrientation(const IO::Astrodynamics::Math::Quaternion &quaternion, const IO::Astrodynamics::Math::Vector3D &angularVelocity, IO::Astrodynamics::Time::TDB epoch, IO::Astrodynamics::Frames::Frames frame) : m_quaternion{quaternion}, m_angularVelocity{angularVelocity}, m_epoch{std::move(epoch)}, m_frame{std::move(frame)}
{
}

IO::Astrodynamics::OrbitalParameters::StateOrientation::StateOrientation(const IO::Astrodynamics::OrbitalParameters::StateOrientation &stateOrientation)
= default;

IO::Astrodynamics::OrbitalParameters::StateOrientation &IO::Astrodynamics::OrbitalParameters::StateOrientation::operator=(const StateOrientation &rhs)
{
	if (this != &rhs)
	{
		const_cast<IO::Astrodynamics::Math::Vector3D &>(m_angularVelocity) = rhs.m_angularVelocity;
		const_cast<IO::Astrodynamics::Time::TDB &>(m_epoch) = rhs.m_epoch;
		const_cast<IO::Astrodynamics::Frames::Frames &>(m_frame) = rhs.m_frame;
		const_cast<IO::Astrodynamics::Math::Quaternion &>(m_quaternion) = rhs.m_quaternion;
	}
	return *this;
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::OrbitalParameters::StateOrientation::GetEpoch() const
{
	return m_epoch;
}

IO::Astrodynamics::Math::Quaternion IO::Astrodynamics::OrbitalParameters::StateOrientation::GetQuaternion() const
{
	return m_quaternion;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::StateOrientation::GetAngularVelocity() const
{
	return m_angularVelocity;
}

IO::Astrodynamics::Frames::Frames IO::Astrodynamics::OrbitalParameters::StateOrientation::GetFrame() const
{
	return m_frame;
}
