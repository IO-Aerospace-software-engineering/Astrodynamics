/**
 * @file OrbitalParameters.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <cmath>
#include <limits>

#include <OrbitalParameters.h>
#include <SDKException.h>
#include <Constants.h>
#include <StateVector.h>
#include <SpiceUsr.h>
#include <InertialFrames.h>

IO::SDK::OrbitalParameters::OrbitalParameters::OrbitalParameters(const std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion, const IO::SDK::Time::TDB &epoch, const IO::SDK::Frames::Frames &frame) : m_centerOfMotion{centerOfMotion}, m_epoch{epoch}, m_frame{frame}
{
}

const std::shared_ptr<IO::SDK::Body::CelestialBody> &IO::SDK::OrbitalParameters::OrbitalParameters::GetCenterOfMotion() const
{
	return m_centerOfMotion;
}

bool IO::SDK::OrbitalParameters::OrbitalParameters::IsElliptical() const
{
	return GetEccentricity() < 1;
}

bool IO::SDK::OrbitalParameters::OrbitalParameters::IsParabolic() const
{
	return GetEccentricity() == 1;
}

bool IO::SDK::OrbitalParameters::OrbitalParameters::IsHyperbolic() const
{
	return GetEccentricity() > 1;
}

bool IO::SDK::OrbitalParameters::OrbitalParameters::IsCircular() const
{
	return GetEccentricity() < IO::SDK::Parameters::CircularEccentricityAccuraccy;
}

double IO::SDK::OrbitalParameters::OrbitalParameters::GetMeanMotion() const
{
	if (this->IsHyperbolic())
	{
		return std::numeric_limits<double>::infinity();
	}

	return IO::SDK::Constants::_2PI / GetPeriod().GetSeconds().count();
}

IO::SDK::Time::TDB IO::SDK::OrbitalParameters::OrbitalParameters::GetTimeToMeanAnomaly(double meanAnomalyTarget) const
{
	double delta{meanAnomalyTarget - GetMeanAnomaly()};
	while (delta < 0.0)
	{
		delta += IO::SDK::Constants::_2PI;
	}
	return IO::SDK::Time::TDB(std::chrono::duration<double>(m_epoch.GetSecondsFromJ2000().count() + std::fmod(delta, IO::SDK::Constants::_2PI) / GetMeanMotion()));
}

IO::SDK::Time::TDB IO::SDK::OrbitalParameters::OrbitalParameters::GetTimeToTrueAnomaly(double trueAnomalyTarget) const
{
	if(trueAnomalyTarget<0.0)
	{
		trueAnomalyTarget+=IO::SDK::Constants::_2PI;
	}
	//X = cos E
	double X = (GetEccentricity() + std::cos(trueAnomalyTarget)) / (1 + GetEccentricity() * std::cos(trueAnomalyTarget));
	double E = std::acos(X);
	double M = E - GetEccentricity() * std::sin(E);

	if (trueAnomalyTarget > IO::SDK::Constants::PI)
	{
		M = IO::SDK::Constants::_2PI - M;
	}

	auto res = GetTimeToMeanAnomaly(M);

	return res;
}

double IO::SDK::OrbitalParameters::OrbitalParameters::GetEccentricAnomaly(IO::SDK::Time::TDB epoch) const
{
	double M{this->GetMeanAnomaly(epoch)};
	double tmpE{M};
	double E{};

	while (std::abs(tmpE - E) > IO::SDK::Constants::ECCENTRIC_ANOMALY_ACCURACY)
	{
		E = tmpE;
		tmpE = M + GetEccentricity() * std::sin(E);
	}
	return E;
}

double IO::SDK::OrbitalParameters::OrbitalParameters::GetMeanAnomaly(IO::SDK::Time::TDB epoch) const
{
	double M{GetMeanAnomaly() + GetMeanMotion() * (epoch - m_epoch).GetSeconds().count()};
	while (M < 0.0)
	{
		M += IO::SDK::Constants::_2PI;
	}
	return std::fmod(M, IO::SDK::Constants::_2PI);
}

double IO::SDK::OrbitalParameters::OrbitalParameters::GetTrueAnomaly(IO::SDK::Time::TDB epoch) const
{
	double E{this->GetEccentricAnomaly(epoch)};
	double v = fmod(atan2(sqrt(1 - pow(GetEccentricity(), 2)) * sin(E), cos(E) - GetEccentricity()), IO::SDK::Constants::_2PI);
	while (v < 0.0)
	{
		v += IO::SDK::Constants::_2PI;
	}
	return std::fmod(v, IO::SDK::Constants::_2PI);
}

double IO::SDK::OrbitalParameters::OrbitalParameters::GetTrueAnomaly() const
{
	return GetTrueAnomaly(m_epoch);
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::OrbitalParameters::GetStateVector() const
{
	return GetStateVector(m_epoch);
}

IO::SDK::Time::TDB IO::SDK::OrbitalParameters::OrbitalParameters::GetEpoch() const
{
	return m_epoch;
}

const IO::SDK::Frames::Frames &IO::SDK::OrbitalParameters::OrbitalParameters::GetFrame() const
{
	return m_frame;
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::OrbitalParameters::GetEccentricityVector() const
{
	auto sv = GetStateVector();
	return (sv.GetVelocity().CrossProduct(GetSpecificAngularMomentum()) / m_centerOfMotion->GetMu()) - (sv.GetPosition() / sv.GetPosition().Magnitude());
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::OrbitalParameters::GetPerigeeVector() const
{
	return GetEccentricityVector().Normalize() * (GetSemiMajorAxis() * (1.0 - GetEccentricity()));
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::OrbitalParameters::GetApogeeVector() const
{
	return GetEccentricityVector().Normalize().Reverse() * (GetSemiMajorAxis() * (1.0 + GetEccentricity()));
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::OrbitalParameters::OrbitalParameters::GetStateVector(const double trueAnomalie) const
{
	return GetStateVector(GetTimeToTrueAnomaly(trueAnomalie));
}

IO::SDK::Math::Vector3D IO::SDK::OrbitalParameters::OrbitalParameters::GetAscendingNodeVector() const
{
	//Compute asending node vector relative to body fixed
	auto v = IO::SDK::Math::Vector3D::VectorZ.CrossProduct(m_frame.TransformVector(m_centerOfMotion->GetBodyFixedFrame(), GetSpecificAngularMomentum(), m_epoch));

	//Transform ascending node vector to original orbital parameter frame
	return m_centerOfMotion->GetBodyFixedFrame().TransformVector(m_frame, v, m_epoch).Normalize();
}

IO::SDK::Coordinates::RADec IO::SDK::OrbitalParameters::OrbitalParameters::GetRADec() const
{
	auto sv = GetStateVector();
	if (sv.GetFrame() != IO::SDK::Frames::InertialFrames::GetICRF())
	{
		sv = sv.ToFrame(IO::SDK::Frames::InertialFrames::GetICRF());
	}

	ConstSpiceDouble rectan[3]{sv.GetPosition().GetX(), sv.GetPosition().GetY(), sv.GetPosition().GetZ()};
	double r, ra, dec;
	recrad_c(rectan, &r, &ra, &dec);

	return IO::SDK::Coordinates::RADec(ra, dec, r);
}

double IO::SDK::OrbitalParameters::OrbitalParameters::GetVelocityAtPerigee() const
{
	return GetStateVector(0.0).GetVelocity().Magnitude();
}

double IO::SDK::OrbitalParameters::OrbitalParameters::GetVelocityAtApogee() const
{
	return GetStateVector(IO::SDK::Constants::PI).GetVelocity().Magnitude();
}