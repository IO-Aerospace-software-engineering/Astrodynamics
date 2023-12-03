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
    double meanAnomaly = sv.GetMeanAnomaly();
    double periapsisArgument = sv.GetPeriapsisArgument();
    double apogee=sv.GetApogeeVector().Magnitude();

    //If target perigee is higher than current apogee
    if (m_peregeeRadius > apogee)
    {
        rp = apogee;
        e = 1 - (2 / ((m_peregeeRadius / rp) + 1));

        //Periapse argument will turn by 180°
        meanAnomaly = std::fmod(meanAnomaly+=Constants::PI,Constants::_2PI);
        periapsisArgument += IO::Astrodynamics::Constants::PI;
    }
    else
    {
        rp = m_peregeeRadius;
        e = 1 - (2 / ((apogee / rp) + 1));
    }

    auto targetOrbit = IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements(sv.GetCenterOfMotion(), rp, e, m_inclination, sv.GetRightAscendingNodeLongitude(), periapsisArgument, meanAnomaly, sv.GetEpoch(), sv.GetFrame());

    return targetOrbit.ToStateVector().GetVelocity() - sv.GetVelocity();
}

IO::Astrodynamics::Math::Vector3D
IO::Astrodynamics::Maneuvers::CombinedManeuver::ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParameters)
{
    return orbitalParameters.GetApogeeVector();
}
