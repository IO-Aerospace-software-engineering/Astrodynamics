/**
 * @file OrbitalPlaneChangingManeuver.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <OrbitalPlaneChangingManeuver.h>
#include <cmath>
#include <Constants.h>
#include <ConicOrbitalElements.h>

IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::OrbitalPlaneChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator), m_targetOrbit{targetOrbit}
{
}

IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::OrbitalPlaneChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit, const IO::SDK::Time::TDB &minimumEpoch) : IO::SDK::Maneuvers::ManeuverBase(engines, propagator, minimumEpoch), m_targetOrbit{targetOrbit}
{
}

bool IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
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

void IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    auto currentvectorState = orbitalParams.GetStateVector();
    //Compute relative inclination
    m_relativeInclination = std::acos(std::cos(orbitalParams.GetInclination()) * std::cos(m_targetOrbit->GetInclination()) + (std::sin(orbitalParams.GetInclination()) * std::sin(m_targetOrbit->GetInclination())) * std::cos(m_targetOrbit->GetRightAscendingNodeLongitude() - orbitalParams.GetRightAscendingNodeLongitude()));

    double rotationAngle = IO::SDK::Constants::PI2 + m_relativeInclination * 0.5;

    if (IsAscendingNode(currentvectorState))
    {
        rotationAngle = -rotationAngle;
    }

    //Compute deltaV
    auto deltaV = 2.0 * currentvectorState.GetVelocity().Magnitude() * std::sin(m_relativeInclination * 0.5);

    IO::SDK::Math::Vector3D positionRotationAxis = currentvectorState.GetPosition().Normalize();
    //Compute the quaternion
    auto q = IO::SDK::Math::Quaternion(positionRotationAxis, rotationAngle);

    //Rotate velocity vector
    auto rotateVecor = currentvectorState.GetVelocity().Rotate(q).Normalize();

    //Compute delta V vector
    m_deltaV = std::make_unique<IO::SDK::Math::Vector3D>(rotateVecor * deltaV);
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::SDK::Math::Vector3D targetVector{maneuverPoint.GetSpecificAngularMomentum().Normalize()};
    if (IsAscendingNode(maneuverPoint.GetStateVector()))
    {
        targetVector = targetVector.Reverse();
    }

    IO::SDK::Math::Quaternion q = m_spacecraft.Front.To(targetVector);

    return IO::SDK::OrbitalParameters::StateOrientation(q, IO::SDK::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(), maneuverPoint.GetFrame());
}

bool IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::IsAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    if (*stateVector.GetCenterOfMotion() != *m_targetOrbit->GetCenterOfMotion())
    {
        throw IO::SDK::Exception::InvalidArgumentException("State vector and target orbit must have the same center of motion");
    }
    auto ANVector = m_targetOrbit->GetSpecificAngularMomentum().CrossProduct(stateVector.GetSpecificAngularMomentum());
    if (ANVector.GetAngle(stateVector.GetPosition()) < IO::SDK::Parameters::NodeDetectionAccuraccy)
    {
        return true;
    }

    return false;
}

bool IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::IsApproachingAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const
{
    if (*stateVector.GetCenterOfMotion() != *m_targetOrbit->GetCenterOfMotion())
    {
        throw IO::SDK::Exception::InvalidArgumentException("State vector and target orbit must have the same center of motion");
    }

    //Ascending node vector
    auto ANVector = m_targetOrbit->GetSpecificAngularMomentum().CrossProduct(stateVector.GetSpecificAngularMomentum());

    //Angle between AN and spacecraft
    double dp = ANVector.DotProduct(stateVector.GetSpecificAngularMomentum().CrossProduct(stateVector.GetPosition()));

    //if < 90° we're in inbound sector
    if (dp > 0.0)
    {
        return true;
    }

    return false;
}

double IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver::GetRelativeInclination() const
{
    return m_relativeInclination;
}