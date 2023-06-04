/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <ConicOrbitalElements.h>

IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::ConicOrbitalElements(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, const double perifocalDistance, const double eccentricity, const double inclination, const double ascendingNodeLongitude, const double periapsisArgument, const double meanAnomaly, const IO::Astrodynamics::Time::TDB &epoch, const IO::Astrodynamics::Frames::Frames &frame) : OrbitalParameters(centerOfMotion, epoch, frame), m_perifocalDistance{perifocalDistance}, m_eccentricity{eccentricity}, m_inclination{inclination}, m_ascendingNodeLongitude{ascendingNodeLongitude}, m_periapsisArgument{periapsisArgument}, m_meanAnomaly{meanAnomaly}
{
	m_semiMajorAxis = -(m_centerOfMotion->GetMu() / (2.0 * GetSpecificOrbitalEnergy()));
	m_orbitalPeriod = IO::Astrodynamics::Time::TimeSpan(std::chrono::duration<double>(IO::Astrodynamics::Constants::_2PI * std::sqrt(std::pow(m_semiMajorAxis, 3.0) / m_centerOfMotion->GetMu())));
	m_trueAnomaly = GetTrueAnomaly(epoch);
}

IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::ConicOrbitalElements(const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion, const double spiceElements[SPICE_OSCLTX_NELTS], const IO::Astrodynamics::Frames::Frames &frame) : OrbitalParameters(centerOfMotion, IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(spiceElements[6])), frame), m_perifocalDistance{spiceElements[0]}, m_eccentricity{spiceElements[1]}, m_inclination{spiceElements[2]}, m_ascendingNodeLongitude{spiceElements[3]}, m_periapsisArgument{spiceElements[4]}, m_meanAnomaly{spiceElements[5]}, m_trueAnomaly{spiceElements[8]}, m_orbitalPeriod{std::chrono::duration<double>(spiceElements[10])}, m_semiMajorAxis{spiceElements[9]}
{
}

IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::ConicOrbitalElements(const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) : OrbitalParameters(stateVector.GetCenterOfMotion(), stateVector.GetEpoch(), stateVector.GetFrame())
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
	m_orbitalPeriod = IO::Astrodynamics::Time::TimeSpan(std::chrono::duration<double>(elts[10]));
	m_semiMajorAxis = elts[9];
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::ToStateVector(const IO::Astrodynamics::Time::TDB &epoch) const
{
	ConstSpiceDouble elts[8] = {m_perifocalDistance, m_eccentricity, m_inclination, m_ascendingNodeLongitude, m_periapsisArgument, m_meanAnomaly, m_epoch.GetSecondsFromJ2000().count(), m_centerOfMotion->GetMu()};
	SpiceDouble state[6] = {};
	conics_c(elts, epoch.GetSecondsFromJ2000().count(), state);

	IO::Astrodynamics::OrbitalParameters::StateVector sv(m_centerOfMotion, state, epoch, m_frame);
	return sv;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetSpecificAngularMomentum() const
{
	return ToStateVector(m_epoch).GetSpecificAngularMomentum();
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetSpecificOrbitalEnergy() const
{
	return ToStateVector(m_epoch).GetSpecificOrbitalEnergy();
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetPerifocalDistance() const
{
	return m_perifocalDistance;
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetEccentricity() const
{
	return m_eccentricity;
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetInclination() const
{
	return m_inclination;
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetRightAscendingNodeLongitude() const
{
	return m_ascendingNodeLongitude;
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetPeriapsisArgument() const
{
	return m_periapsisArgument;
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetMeanAnomaly() const
{
	return m_meanAnomaly;
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetTrueAnomaly() const
{
	return m_trueAnomaly;
}

double IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetSemiMajorAxis() const
{
	return m_semiMajorAxis;
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements::GetPeriod() const
{
	return m_orbitalPeriod;
}