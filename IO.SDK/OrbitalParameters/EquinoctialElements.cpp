#include "EquinoctialElements.h"
#include "Constants.h"
#include <chrono>

IO::SDK::OrbitalParameters::EquinoctialElements::EquinoctialElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Time::TDB &epoch, const double semiMajorAxis, const double h, const double k, const double p, const double q, const double L, const double periapsisLongitudeRate, const double ascendingNodeLongitudeRate, const double rightAscensionOfThePole, const double declinationOfThePole, const IO::SDK::Frames::Frames &frame) : m_semiMajorAxis{semiMajorAxis},
																																																																																																																													m_h{h},
																																																																																																																													m_k{k},
																																																																																																																													m_p{p},
																																																																																																																													m_q{q},
																																																																																																																													m_L{L},
																																																																																																																													m_periapsisLongitudeRate{periapsisLongitudeRate},
																																																																																																																													m_ascendingNodeLongitudeRate{ascendingNodeLongitudeRate},
																																																																																																																													m_rightAscensionOfThePole{rightAscensionOfThePole},
																																																																																																																													m_declinationOfThePole{declinationOfThePole},
																																																																																																																													OrbitalParameters(centerOfMotion, epoch, frame)
{
	const_cast<double &>(m_meanAnomalyRate) = std::sqrt(centerOfMotion->GetMu() / semiMajorAxis) / semiMajorAxis;

	m_elements[0] = semiMajorAxis;
	m_elements[1] = h;
	m_elements[2] = k;
	m_elements[3] = L;
	m_elements[4] = p;
	m_elements[5] = q;
	m_elements[6] = m_periapsisLongitudeRate;
	m_elements[7] = m_meanAnomalyRate + m_periapsisLongitudeRate + m_ascendingNodeLongitudeRate;
	m_elements[8] = m_ascendingNodeLongitudeRate;

	m_period = IO::SDK::Time::TimeSpan(std::chrono::duration<double>(IO::SDK::Constants::_2PI * (std::sqrt(std::pow(semiMajorAxis, 3) / centerOfMotion->GetMu()))));
}

IO::SDK::OrbitalParameters::EquinoctialElements::EquinoctialElements(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const double semiMajorAxis, const double eccentricity, const double inclination, const double peregeeArgument, const double longitudeAN, const double meanAnomaly, const double periapsisLongitudeRate, const double ascendingNodeLongitudeRate, const double rightAscensionOfThePole, const double declinationOfThePole, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame) : m_semiMajorAxis{semiMajorAxis}, m_periapsisLongitudeRate{periapsisLongitudeRate}, m_rightAscensionOfThePole{rightAscensionOfThePole}, m_declinationOfThePole{declinationOfThePole}, m_ascendingNodeLongitudeRate{ascendingNodeLongitudeRate}, OrbitalParameters(centerOfMotion, epoch, frame)
{

	const_cast<double &>(m_h) = eccentricity * sin(peregeeArgument + longitudeAN);
	const_cast<double &>(m_k) = eccentricity * cos(peregeeArgument + longitudeAN);
	const_cast<double &>(m_p) = tan(inclination * 0.5) * sin(longitudeAN);
	const_cast<double &>(m_q) = tan(inclination * 0.5) * cos(longitudeAN);
	const_cast<double &>(m_L) = meanAnomaly + peregeeArgument + longitudeAN;
	const_cast<double &>(m_meanAnomalyRate) = std::sqrt(centerOfMotion->GetMu() / semiMajorAxis) / semiMajorAxis;
	

	m_elements[0] = semiMajorAxis;
	m_elements[1] = m_h;
	m_elements[2] = m_k;
	m_elements[3] = m_L;
	m_elements[4] = m_p;
	m_elements[5] = m_q;
	m_elements[6] = m_periapsisLongitudeRate;
	m_elements[7] = m_meanAnomalyRate + m_periapsisLongitudeRate + m_ascendingNodeLongitudeRate;
	m_elements[8] = m_ascendingNodeLongitudeRate;
	m_period = IO::SDK::Time::TimeSpan(std::chrono::duration<double>(IO::SDK::Constants::_2PI * (std::sqrt(std::pow(semiMajorAxis, 3) / centerOfMotion->GetMu()))));
}

IO::SDK::Time::TimeSpan IO::SDK::OrbitalParameters::EquinoctialElements::GetPeriod() const
{
	return m_period;
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::EquinoctialElements::GetSpecificAngularMomentum() const
{
	return GetStateVector(m_epoch).GetSpecificAngularMomentum();
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::EquinoctialElements::GetStateVector(const IO::SDK::Time::TDB &epoch) const
{
	double state[6]{};
	eqncpv_c(epoch.GetSecondsFromJ2000().count(), m_epoch.GetSecondsFromJ2000().count(), m_elements, m_rightAscensionOfThePole, m_declinationOfThePole, state);
	return StateVector(m_centerOfMotion, IO::SDK::Math::Vector3D(state[0], state[1], state[2]), IO::SDK::Math::Vector3D(state[3], state[4], state[5]), epoch, m_frame);
}

double IO::SDK::OrbitalParameters::EquinoctialElements::GetEccentricity() const
{
	return sqrt(m_h * m_h + m_k * m_k);
}

double IO::SDK::OrbitalParameters::EquinoctialElements::GetInclination() const
{
	return atan2(2 * sqrt(m_q * m_q + m_p * m_p), 1 - m_q * m_q - m_p * m_p);
}

double IO::SDK::OrbitalParameters::EquinoctialElements::GetPeriapsisArgument() const
{
	return atan2(m_h * m_q - m_k * m_p, m_k * m_q + m_h * m_p);
}

double IO::SDK::OrbitalParameters::EquinoctialElements::GetRightAscendingNodeLongitude() const
{
	return atan2(m_p, m_q);
}

double IO::SDK::OrbitalParameters::EquinoctialElements::GetMeanAnomaly() const
{
	return m_L - atan2(m_h, m_k);
}

double IO::SDK::OrbitalParameters::EquinoctialElements::GetSpecificOrbitalEnergy() const
{
	return GetStateVector(m_epoch).GetSpecificOrbitalEnergy();
}
