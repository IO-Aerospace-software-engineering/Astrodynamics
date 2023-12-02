/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <CombinedManeuver.h>
#include <ConicOrbitalElements.h>
#include <Vector3D.h>
#include <Parameters.h>

#include <utility>

IO::Astrodynamics::Maneuvers::CombinedManeuver::CombinedManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius) : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator), m_inclination{inclination}, m_peregeeRadius{perigeeRadius}
{
}

IO::Astrodynamics::Maneuvers::CombinedManeuver::CombinedManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius, const IO::Astrodynamics::Time::TDB &minimumEpoch) : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, minimumEpoch), m_inclination{inclination}, m_peregeeRadius{perigeeRadius}
{
}

bool IO::Astrodynamics::Maneuvers::CombinedManeuver::CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
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

void IO::Astrodynamics::Maneuvers::CombinedManeuver::Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    //Compute delta V vector
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>(GetDeltaV(orbitalParams.ToStateVector()));
}

IO::Astrodynamics::OrbitalParameters::StateOrientation IO::Astrodynamics::Maneuvers::CombinedManeuver::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    auto maneuverVelocity = GetDeltaV(maneuverPoint.ToStateVector());

    return IO::Astrodynamics::OrbitalParameters::StateOrientation{maneuverVelocity.Normalize().To(m_spacecraft.Front), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame()};
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Maneuvers::CombinedManeuver::GetDeltaV(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const
{
    double e{};
    double rp{};
    double meanAnomaly = IO::Astrodynamics::Constants::PI;
    double periapsisArgument = sv.GetPeriapsisArgument();

    //If target perigee is higher than current apogee
    if (m_peregeeRadius > sv.GetApogeeVector().Magnitude())
    {
        rp = sv.GetApogeeVector().Magnitude();
        e = 1 - (2 / ((m_peregeeRadius / rp) + 1));

        //Periapse argument will turn by 180°
        meanAnomaly = 0.0;
        periapsisArgument += IO::Astrodynamics::Constants::PI;
    }
    else
    {
        rp = m_peregeeRadius;
        e = 1 - (2 / ((sv.GetApogeeVector().Magnitude() / rp) + 1));
    }

    auto targetOrbit = IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements(sv.GetCenterOfMotion(), rp, e, m_inclination, meanAnomaly, periapsisArgument, 0.0, sv.GetEpoch(), sv.GetFrame());

    return targetOrbit.ToStateVector().GetVelocity() - sv.GetVelocity();
}

IO::Astrodynamics::Math::Vector3D
IO::Astrodynamics::Maneuvers::CombinedManeuver::ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParameters)
{
    return orbitalParameters.GetApogeeVector();
}
