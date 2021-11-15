/**
 * @file ApsidalAlignmentManeuver.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <ApsidalAlignmentManeuver.h>
#include <SDKException.h>
#include <cmath>
#include <ConicOrbitalElements.h>

IO::SDK::Maneuvers::ApsidalAlignmentManeuver::ApsidalAlignmentManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator), m_targetOrbit{targetOrbit}
{
}

IO::SDK::Maneuvers::ApsidalAlignmentManeuver::ApsidalAlignmentManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit, const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch), m_targetOrbit{targetOrbit}
{
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    bool resP = false;
    bool resQ = false;

    //Q POINT
    bool isApproachingQ = IsApproachingIntersectPointQ(orbitalParams.GetStateVector());

    //Initialize status
    if (!m_isApproachingQ)
    {
        m_isApproachingQ = std::make_unique<bool>(isApproachingQ);
        return false;
    }

    //Check status changing
    if (isApproachingQ != *m_isApproachingQ)
    {
        *m_isApproachingQ = isApproachingQ;
        resQ = !*m_isApproachingQ;
        if (resQ)
        {
            m_isIntersectQ = true;
            return true;
        }
    }

    // P POINT
    bool isApproachingP = IsApproachingIntersectPointP(orbitalParams.GetStateVector());

    if (!m_isApproachingP)
    {
        m_isApproachingP = std::make_unique<bool>(isApproachingP);
        return false;
    }

    if (isApproachingP != *m_isApproachingP)
    {
        *m_isApproachingP = isApproachingP;
        resP = !*m_isApproachingP;
        if (resP)
        {
            m_isIntersectP = true;
            return true;
        }
    }

    return false;
}

void IO::SDK::Maneuvers::ApsidalAlignmentManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(GetDeltaV(orbitalParams.GetStateVector()));
    m_theta = GetTheta(orbitalParams.GetStateVector());
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::ApsidalAlignmentManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::SDK::Math::Vector3D resVector = GetDeltaV(maneuverPoint.GetStateVector());

    return IO::SDK::OrbitalParameters::StateOrientation(m_spacecraft.Front.To(resVector.Normalize()), IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::IsIntersectP(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    double v = GetPTrueAnomaly(stateVector);

    auto v_vector = stateVector.GetStateVector(v).GetPosition();
    if (v_vector.GetAngle(stateVector.GetPosition()) < IO::SDK::Parameters::IntersectDetectionAccuraccy)
    {
        return true;
    }

    return false;
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::IsIntersectQ(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    double v = GetQTrueAnomaly(stateVector);

    auto v_vector = stateVector.GetStateVector(v).GetPosition();
    if (v_vector.GetAngle(stateVector.GetPosition()) < IO::SDK::Parameters::IntersectDetectionAccuraccy)
    {
        return true;
    }

    return false;
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::IsApproachingIntersectPointP(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    double v = GetPTrueAnomaly(stateVector);

    auto v_vector = stateVector.GetStateVector(v).GetPosition();

    if (v_vector.DotProduct(stateVector.GetSpecificAngularMomentum().CrossProduct(stateVector.GetPosition())) > 0)
    {
        return true;
    }

    return false;
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::IsApproachingIntersectPointQ(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    double v = GetQTrueAnomaly(stateVector);

    auto v_vector = stateVector.GetStateVector(v).GetPosition();

    if (v_vector.DotProduct(stateVector.GetSpecificAngularMomentum().CrossProduct(stateVector.GetPosition())) > 0)
    {
        return true;
    }

    return false;
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetTheta() const
{
    return m_theta;
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetTheta(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    return stateVector.GetPerigeeVector().GetAngle(m_targetOrbit->GetPerigeeVector());
}

std::map<std::string, double> IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetCoefficients(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
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

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetPTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const
{
    auto coef = GetCoefficients(sv);
    double res = coef["alpha"] + std::acos((coef["C"] / coef["A"]) * std::cos(coef["alpha"]));
    if (res < 0.0)
    {
        res += IO::SDK::Constants::_2PI;
    }
    return res;
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetQTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const
{
    auto coef = GetCoefficients(sv);
    double res = coef["alpha"] - std::acos((coef["C"] / coef["A"]) * std::cos(coef["alpha"]));
    if (res < 0.0)
    {
        res += IO::SDK::Constants::_2PI;
    }
    return res;
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetPTargetTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const
{
    return GetPTrueAnomaly(sv) - GetTheta(sv);
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetQTargetTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const
{
    return GetQTrueAnomaly(sv) - GetTheta(sv);
}

IO::SDK::Math::Vector3D IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetDeltaV(const IO::SDK::OrbitalParameters::StateVector &sv) const
{
    if (m_deltaV)
    {
        return *m_deltaV;
    }

    IO::SDK::Math::Vector3D resVector;
    if (m_isIntersectP)
    {
        resVector = m_targetOrbit->GetStateVector(GetPTargetTrueAnomaly(sv)).GetVelocity() - sv.GetVelocity();
    }
    else if (m_isIntersectQ)
    {
        resVector = m_targetOrbit->GetStateVector(GetQTargetTrueAnomaly(sv)).GetVelocity() - sv.GetVelocity();
    }
    else
    {
        throw IO::SDK::Exception::InvalidArgumentException("To compute orientation, maneuver point must be at orbits intersection");
    }

    return resVector;
}