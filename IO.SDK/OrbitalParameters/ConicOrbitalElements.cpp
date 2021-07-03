/**
 * @file ConicOrbitalElements.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <ConicOrbitalElements.h>
#include <cmath>
#include <Vector3D.h>
#include <iostream>
#include <SpiceUsr.h>
#include <chrono>

IO::SDK::OrbitalParameters::ConicOrbitalElements::ConicOrbitalElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const double perifocalDistance, const double eccentricity, const double inclination, const double ascendingNodeLongitude, const double periapsisArgument, const double meanAnomaly, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame) : OrbitalParameters(centerOfMotion, epoch, frame), m_perifocalDistance{perifocalDistance}, m_eccentricity{eccentricity}, m_inclination{inclination}, m_ascendingNodeLongitude{ascendingNodeLongitude}, m_periapsisArgument{periapsisArgument}, m_meanAnomaly{meanAnomaly}
{
	m_semiMajorAxis = -(m_centerOfMotion->GetMu() / (2.0 * GetSpecificOrbitalEnergy()));
	m_orbitalPeriod = IO::SDK::Time::TimeSpan(std::chrono::duration<double>(IO::SDK::Constants::_2PI * std::sqrt(std::pow(m_semiMajorAxis, 3.0) / m_centerOfMotion->GetMu())));
	m_trueAnomaly = GetTrueAnomaly(epoch);
}

IO::SDK::OrbitalParameters::ConicOrbitalElements::ConicOrbitalElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const double spiceElements[SPICE_OSCLTX_NELTS], const IO::SDK::Frames::Frames &frame) : OrbitalParameters(centerOfMotion, IO::SDK::Time::TDB(std::chrono::duration<double>(spiceElements[6])), frame), m_perifocalDistance{spiceElements[0]}, m_eccentricity{spiceElements[1]}, m_inclination{spiceElements[2]}, m_ascendingNodeLongitude{spiceElements[3]}, m_periapsisArgument{spiceElements[4]}, m_meanAnomaly{spiceElements[5]}, m_trueAnomaly{spiceElements[8]}, m_orbitalPeriod{std::chrono::duration<double>(spiceElements[10])}, m_semiMajorAxis{spiceElements[9]}
{
}

IO::SDK::OrbitalParameters::ConicOrbitalElements::ConicOrbitalElements(const IO::SDK::OrbitalParameters::StateVector &stateVector) : OrbitalParameters(stateVector.GetCenterOfMotion(), stateVector.GetEpoch(), stateVector.GetFrame())
{
	SpiceDouble elts[SPICE_OSCLTX_NELTS];
	ConstSpiceDouble state[6]{stateVector.GetPosition().GetX(), stateVector.GetPosition().GetY(), stateVector.GetPosition().GetZ(), stateVector.GetVelocity().GetX(), stateVector.GetVelocity().GetY(), stateVector.GetVelocity().GetZ()};
	oscltx_c(state, stateVector.GetEpoch().GetSecondsFromJ2000().count(), stateVector.GetCenterOfMotion()->GetMu(), elts);
	m_perifocalDistance = elts[0];
	m_eccentricity = elts[1];
	m_inclination = elts[2];
	m_ascendingNodeLongitude = elts[3];
	m_periapsisArgument = elts[4];
	m_meanAnomaly = elts[5];
	m_trueAnomaly = elts[8];
	m_orbitalPeriod = IO::SDK::Time::TimeSpan(std::chrono::duration<double>(elts[10]));
	m_semiMajorAxis = elts[9];
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::ConicOrbitalElements::GetStateVector(const IO::SDK::Time::TDB &epoch) const
{
	ConstSpiceDouble elts[8] = {m_perifocalDistance, m_eccentricity, m_inclination, m_ascendingNodeLongitude, m_periapsisArgument, m_meanAnomaly, m_epoch.GetSecondsFromJ2000().count(), m_centerOfMotion->GetMu()};
	SpiceDouble state[6] = {};
	conics_c(elts, epoch.GetSecondsFromJ2000().count(), state);

	IO::SDK::OrbitalParameters::StateVector sv(m_centerOfMotion, state, epoch, m_frame);
	return sv;
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::ConicOrbitalElements::GetSpecificAngularMomentum() const
{
	return GetStateVector(m_epoch).GetSpecificAngularMomentum();
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetSpecificOrbitalEnergy() const
{
	return GetStateVector(m_epoch).GetSpecificOrbitalEnergy();
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetPerifocalDistance() const
{
	return m_perifocalDistance;
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetEccentricity() const
{
	return m_eccentricity;
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetInclination() const
{
	return m_inclination;
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetRightAscendingNodeLongitude() const
{
	return m_ascendingNodeLongitude;
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetPeriapsisArgument() const
{
	return m_periapsisArgument;
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetMeanAnomaly() const
{
	return m_meanAnomaly;
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetTrueAnomaly() const
{
	return m_trueAnomaly;
}

double IO::SDK::OrbitalParameters::ConicOrbitalElements::GetSemiMajorAxis() const
{
	return m_semiMajorAxis;
}

IO::SDK::Time::TimeSpan IO::SDK::OrbitalParameters::ConicOrbitalElements::GetPeriod() const
{
	return m_orbitalPeriod;
}