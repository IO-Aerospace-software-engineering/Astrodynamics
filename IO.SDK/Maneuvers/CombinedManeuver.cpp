/**
 * @file CombinedManeuver.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <CombinedManeuver.h>
#include <ConicOrbitalElements.h>
#include <Vector3D.h>
#include <Parameters.h>

IO::SDK::Maneuvers::CombinedManeuver::CombinedManeuver(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator), m_inclination{inclination}, m_peregeeRadius{perigeeRadius}
{
}

IO::SDK::Maneuvers::CombinedManeuver::CombinedManeuver(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius, const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch), m_inclination{inclination}, m_peregeeRadius{perigeeRadius}
{
}

bool IO::SDK::Maneuvers::CombinedManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    //Check apsidal apogee vector and node line are aligned
    auto ANVectorDirection = orbitalParams.GetAscendingNodeVector().Normalize();
    auto DNVectorDirection = ANVectorDirection.Reverse();
    auto apogeeVector = orbitalParams.GetApogeeVector().Normalize();
    if (std::abs(ANVectorDirection.DotProduct(apogeeVector)) < 0.9 && std::abs(DNVectorDirection.DotProduct(apogeeVector)) < 0.9)
    {
        return false;
    }

    if (orbitalParams.IsCircular() || (orbitalParams.GetMeanAnomaly() >= Constants::PI && orbitalParams.GetMeanAnomaly() < Constants::PI + Parameters::NodeDetectionAccuraccy))
    {
        return true;
    }

    return false;
}

void IO::SDK::Maneuvers::CombinedManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    //Compute delta V vector
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(GetDeltaV(orbitalParams.GetStateVector()));
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::CombinedManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    auto maneuverVelocity = GetDeltaV(maneuverPoint.GetStateVector());

    return IO::SDK::OrbitalParameters::StateOrientation{maneuverVelocity.Normalize().To(m_spacecraft.Front), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame()};
}

IO::SDK::Math::Vector3D IO::SDK::Maneuvers::CombinedManeuver::GetDeltaV(const IO::SDK::OrbitalParameters::StateVector &sv) const
{
    double e{};
    double rp{};
    double meanAnomaly = IO::SDK::Constants::PI;
    double periapsisArgument = sv.GetPeriapsisArgument();

    //If target perigee is higher than current apogee
    if (m_peregeeRadius > sv.GetApogeeVector().Magnitude())
    {
        rp = sv.GetApogeeVector().Magnitude();
        e = 1 - (2 / ((m_peregeeRadius / rp) + 1));

        //Periapse argument will turn by 180°
        meanAnomaly = 0.0;
        periapsisArgument += IO::SDK::Constants::PI;
    }
    else
    {
        rp = m_peregeeRadius;
        e = 1 - (2 / ((sv.GetApogeeVector().Magnitude() / rp) + 1));
    }

    auto targetOrbit = IO::SDK::OrbitalParameters::ConicOrbitalElements(sv.GetCenterOfMotion(), rp, e, m_inclination, 0.0, periapsisArgument, 0.0, sv.GetEpoch(), sv.GetFrame());

    return targetOrbit.GetStateVector(meanAnomaly).GetVelocity() - sv.GetVelocity();
}