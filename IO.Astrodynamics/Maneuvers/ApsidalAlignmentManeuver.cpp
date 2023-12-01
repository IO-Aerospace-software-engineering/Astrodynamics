/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <ApsidalAlignmentManeuver.h>
#include <Parameters.h>
#include <Tools.h>

IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::ApsidalAlignmentManeuver(
        std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
        std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit) : IO::Astrodynamics::Maneuvers::ManeuverBase(
        std::move(engines), propagator), m_targetOrbit{std::move(targetOrbit)}
{
}

IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::ApsidalAlignmentManeuver(
        std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines, IO::Astrodynamics::Propagators::Propagator &propagator,
        std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit,
        const IO::Astrodynamics::Time::TDB &minimumEpoch) : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator,
                                                                                                       minimumEpoch),
                                                            m_targetOrbit{std::move(targetOrbit)}
{
}

bool IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{

}

void IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::Compute(
        const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>(GetDeltaV(orbitalParams.ToStateVector()));
    m_theta = GetTheta(orbitalParams.ToStateVector());
}

IO::Astrodynamics::OrbitalParameters::StateOrientation IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::ComputeOrientation(
        const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::Astrodynamics::Math::Vector3D resVector = GetDeltaV(maneuverPoint.ToStateVector());

    return IO::Astrodynamics::OrbitalParameters::StateOrientation{resVector.Normalize().To(m_spacecraft.Front),
                                                                  IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0),
                                                                  maneuverPoint.GetEpoch(), maneuverPoint.GetFrame()};
}

bool IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::IsIntersectP(
        const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const
{
    double v = GetPTrueAnomaly(stateVector);

    auto v_vector = stateVector.ToStateVector(v).GetPosition();
    if (v_vector.GetAngle(stateVector.GetPosition()) < IO::Astrodynamics::Parameters::IntersectDetectionAccuraccy)
    {
        return true;
    }

    return false;
}

bool IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::IsIntersectQ(
        const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const
{
    double v = GetQTrueAnomaly(stateVector);

    auto v_vector = stateVector.ToStateVector(v).GetPosition();
    if (v_vector.GetAngle(stateVector.GetPosition()) < IO::Astrodynamics::Parameters::IntersectDetectionAccuraccy)
    {
        return true;
    }

    return false;
}

double IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetTheta() const
{
    return m_theta;
}

double IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetTheta(
        const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const
{
    return stateVector.GetPerigeeVector().GetAngle(m_targetOrbit->GetPerigeeVector());
}

std::map<std::string, double> IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetCoefficients(
        const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const
{
    std::map<std::string, double> res;
    double h1 = std::pow(stateVector.GetSpecificAngularMomentum().Magnitude(), 2.0);
    double h2 = std::pow(m_targetOrbit->GetSpecificAngularMomentum().Magnitude(), 2.0);
    double theta = GetTheta(stateVector);

    res["A"] = h2 * stateVector.GetEccentricity() - h1 * m_targetOrbit->GetEccentricity() * std::cos(theta);
    res["B"] = -h1 * m_targetOrbit->GetEccentricity() * std::sin(theta);
    res["C"] = h1 - h2;
    res["alpha"] = std::atan(res["B"] / res["A"]);

    return res;
}

double
IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetPTrueAnomaly(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const
{
    auto coef = GetCoefficients(sv);
    double res = coef["alpha"] + std::acos((coef["C"] / coef["A"]) * std::cos(coef["alpha"]));
    if (std::isnan(res))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Apsidal alignment requieres orbits intersection");
    }

    if (res < 0.0)
    {
        res += IO::Astrodynamics::Constants::_2PI;
    }
    return res;
}

double
IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetQTrueAnomaly(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const
{
    auto coef = GetCoefficients(sv);
    double res = coef["alpha"] - std::acos((coef["C"] / coef["A"]) * std::cos(coef["alpha"]));
    if (std::isnan(res))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Apsidal alignment requieres orbits intersection");
    }

    if (res < 0.0)
    {
        res += IO::Astrodynamics::Constants::_2PI;
    }
    return res;
}

double IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetPTargetTrueAnomaly(
        const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const
{
    return GetPTrueAnomaly(sv) - GetTheta(sv);
}

double IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetQTargetTrueAnomaly(
        const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const
{
    return GetQTrueAnomaly(sv) - GetTheta(sv);
}

IO::Astrodynamics::Math::Vector3D
IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::GetDeltaV(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const
{
    if (m_deltaV)
    {
        return *m_deltaV;
    }

    IO::Astrodynamics::Math::Vector3D resVector;
    if (m_isIntersectP)
    {
        resVector = m_targetOrbit->ToStateVector(GetPTargetTrueAnomaly(sv)).GetVelocity() - sv.GetVelocity();
    } else if (m_isIntersectQ)
    {
        resVector = m_targetOrbit->ToStateVector(GetQTargetTrueAnomaly(sv)).GetVelocity() - sv.GetVelocity();
    } else
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException(
                "To compute orientation, maneuver point must be at orbits intersection");
    }

    return resVector;
}


IO::Astrodynamics::Math::Vector3D
IO::Astrodynamics::Maneuvers::ApsidalAlignmentManeuver::ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    auto sv = orbitalParams.ToStateVector();
    double pv = GetPTrueAnomaly(sv);
    double qv = GetQTrueAnomaly(sv);
    double v = orbitalParams.GetTrueAnomaly();
    double vRelativeToP = AngleDifference(v, pv);
    double vRelativeToQ = AngleDifference(v, qv);

    if (vRelativeToP <= vRelativeToQ)
    {
        m_isIntersectP = true;
        m_isIntersectQ = false;
        return orbitalParams.ToStateVector(pv).GetPosition();
    }

    m_isIntersectP = false;
    m_isIntersectQ = true;
    return orbitalParams.ToStateVector(qv).GetPosition();
}
