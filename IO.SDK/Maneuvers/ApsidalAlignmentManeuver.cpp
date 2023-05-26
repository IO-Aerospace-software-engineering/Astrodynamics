/**
 * @file ApsidalAlignmentManeuver.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <ApsidalAlignmentManeuver.h>
#include <ConicOrbitalElements.h>
#include <Parameters.h>

#include <utility>

IO::SDK::Maneuvers::ApsidalAlignmentManeuver::ApsidalAlignmentManeuver(
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines, IO::SDK::Propagators::Propagator &propagator,
        std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> targetOrbit) : IO::SDK::Maneuvers::ManeuverBase(
        std::move(engines), propagator), m_targetOrbit{std::move(targetOrbit)} {
}

IO::SDK::Maneuvers::ApsidalAlignmentManeuver::ApsidalAlignmentManeuver(
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines, IO::SDK::Propagators::Propagator &propagator,
        std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> targetOrbit,
        const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(std::move(engines), propagator,
                                                                                   minimumEpoch),
                                                  m_targetOrbit{std::move(targetOrbit)} {
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::CanExecute(
        const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) {
    double pv = GetPTrueAnomaly(orbitalParams.GetStateVector());
    double qv = GetQTrueAnomaly(orbitalParams.GetStateVector());
    double v = orbitalParams.GetTrueAnomaly();
    double vRelativeToP = v - pv;
    double vRelativeToQ = v - qv;

    if (vRelativeToP < 0.0)
        vRelativeToP += Constants::_2PI;
    if (vRelativeToQ < 0.0)
        vRelativeToQ += Constants::_2PI;

    vRelativeToP = std::fmod(vRelativeToP, Constants::_2PI);
    vRelativeToQ = std::fmod(vRelativeToQ, Constants::_2PI);

    //TODO:manage case where pv or pq == 359° and Tolerance + 2° (that does mean uppervalue ==361°)
    if (vRelativeToP < Parameters::NodeDetectionAccuraccy) {
        m_isIntersectP = true;
        return true;
    } else if (vRelativeToQ < Parameters::NodeDetectionAccuraccy) {
        m_isIntersectQ = true;
        return true;
    }
    return false;
}

void IO::SDK::Maneuvers::ApsidalAlignmentManeuver::Compute(
        const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) {
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(GetDeltaV(orbitalParams.GetStateVector()));
    m_theta = GetTheta(orbitalParams.GetStateVector());
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::ApsidalAlignmentManeuver::ComputeOrientation(
        const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) {
    IO::SDK::Math::Vector3D resVector = GetDeltaV(maneuverPoint.GetStateVector());

    return IO::SDK::OrbitalParameters::StateOrientation{resVector.Normalize().To(m_spacecraft.Front),
                                                        IO::SDK::Math::Vector3D(0.0, 0.0, 0.0),
                                                        maneuverPoint.GetEpoch(), maneuverPoint.GetFrame()};
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::IsIntersectP(
        const IO::SDK::OrbitalParameters::StateVector &stateVector) const {
    double v = GetPTrueAnomaly(stateVector);

    auto v_vector = stateVector.GetStateVector(v).GetPosition();
    if (v_vector.GetAngle(stateVector.GetPosition()) < IO::SDK::Parameters::IntersectDetectionAccuraccy) {
        return true;
    }

    return false;
}

bool IO::SDK::Maneuvers::ApsidalAlignmentManeuver::IsIntersectQ(
        const IO::SDK::OrbitalParameters::StateVector &stateVector) const {
    double v = GetQTrueAnomaly(stateVector);

    auto v_vector = stateVector.GetStateVector(v).GetPosition();
    if (v_vector.GetAngle(stateVector.GetPosition()) < IO::SDK::Parameters::IntersectDetectionAccuraccy) {
        return true;
    }

    return false;
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetTheta() const {
    return m_theta;
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetTheta(
        const IO::SDK::OrbitalParameters::StateVector &stateVector) const {
    return stateVector.GetPerigeeVector().GetAngle(m_targetOrbit->GetPerigeeVector());
}

std::map<std::string, double> IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetCoefficients(
        const IO::SDK::OrbitalParameters::StateVector &stateVector) const {
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
IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetPTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const {
    auto coef = GetCoefficients(sv);
    double res = coef["alpha"] + std::acos((coef["C"] / coef["A"]) * std::cos(coef["alpha"]));
    if (std::isnan(res)) {
        throw IO::SDK::Exception::InvalidArgumentException("Apsidal alignment requieres orbits intersection");
    }

    if (res < 0.0) {
        res += IO::SDK::Constants::_2PI;
    }
    return res;
}

double
IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetQTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const {
    auto coef = GetCoefficients(sv);
    double res = coef["alpha"] - std::acos((coef["C"] / coef["A"]) * std::cos(coef["alpha"]));
    if (std::isnan(res)) {
        throw IO::SDK::Exception::InvalidArgumentException("Apsidal alignment requieres orbits intersection");
    }

    if (res < 0.0) {
        res += IO::SDK::Constants::_2PI;
    }
    return res;
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetPTargetTrueAnomaly(
        const IO::SDK::OrbitalParameters::StateVector &sv) const {
    return GetPTrueAnomaly(sv) - GetTheta(sv);
}

double IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetQTargetTrueAnomaly(
        const IO::SDK::OrbitalParameters::StateVector &sv) const {
    return GetQTrueAnomaly(sv) - GetTheta(sv);
}

IO::SDK::Math::Vector3D
IO::SDK::Maneuvers::ApsidalAlignmentManeuver::GetDeltaV(const IO::SDK::OrbitalParameters::StateVector &sv) const {
    if (m_deltaV) {
        return *m_deltaV;
    }

    IO::SDK::Math::Vector3D resVector;
    if (m_isIntersectP) {
        resVector = m_targetOrbit->GetStateVector(GetPTargetTrueAnomaly(sv)).GetVelocity() - sv.GetVelocity();
    } else if (m_isIntersectQ) {
        resVector = m_targetOrbit->GetStateVector(GetQTargetTrueAnomaly(sv)).GetVelocity() - sv.GetVelocity();
    } else {
        throw IO::SDK::Exception::InvalidArgumentException(
                "To compute orientation, maneuver point must be at orbits intersection");
    }

    return resVector;
}