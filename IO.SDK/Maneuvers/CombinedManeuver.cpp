#include <CombinedManeuver.h>
#include <ConicOrbitalElements.h>
#include <Vector3D.h>

IO::SDK::Maneuvers::CombinedManeuver::CombinedManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius) : m_inclination{inclination}, m_peregeeRadius{perigeeRadius}, IO::SDK::Maneuvers::ManeuverBase(engines, propagator)
{
}

IO::SDK::Maneuvers::CombinedManeuver::CombinedManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius, const IO::SDK::Time::TDB &minimumEpoch) : m_inclination{inclination}, m_peregeeRadius{perigeeRadius}, IO::SDK::Maneuvers::ManeuverBase(engines,  propagator, minimumEpoch)
{
}

bool IO::SDK::Maneuvers::CombinedManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    //Check apsidal apogee vector and node line are aligned
    auto ANVectorDirection = orbitalParams.GetAscendingNodeVector();
    if (ANVectorDirection.CrossProduct(orbitalParams.GetStateVector().GetApogeeVector().Normalize()).Magnitude() > 0.1)
    {
        return false;
    }

    bool isApproachingNode = IsApproachingAscendingNode(orbitalParams.GetStateVector());

    if (!m_isApproachingNode)
    {
        m_isApproachingNode = std::make_unique<bool>(isApproachingNode);
        return false;
    }

    if (isApproachingNode != *m_isApproachingNode)
    {
        *m_isApproachingNode = isApproachingNode;
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

    auto q = m_spacecraft.Front.To(maneuverVelocity.Normalize());

    return IO::SDK::OrbitalParameters::StateOrientation(q, IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::CombinedManeuver::IsAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    if (stateVector.ToBodyFixedFrame().GetVelocity().GetZ() > 0.0)
    {
        return true;
    }

    return false;
}

bool IO::SDK::Maneuvers::CombinedManeuver::IsApproachingAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    //Ascending node vector
    auto ANVector = stateVector.GetAscendingNodeVector();

    //Angle between AN and spacecraft
    double dp = ANVector.DotProduct(stateVector.GetSpecificAngularMomentum().Normalize().CrossProduct(stateVector.GetPosition().Normalize()));

    //if < 90° we're in inbound sector
    if (dp > 0.0)
    {
        return true;
    }

    return false;
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