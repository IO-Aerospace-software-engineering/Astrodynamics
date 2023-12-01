/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <OrbitalPlaneChangingManeuver.h>
#include <Constants.h>
#include <Parameters.h>

#include <utility>

IO::Astrodynamics::Maneuvers::OrbitalPlaneChangingManeuver::OrbitalPlaneChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines,
                                                                                         IO::Astrodynamics::Propagators::Propagator &propagator,
                                                                                         std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(
        std::move(engines), propagator), m_targetOrbit{std::move(targetOrbit)}
{
}

IO::Astrodynamics::Maneuvers::OrbitalPlaneChangingManeuver::OrbitalPlaneChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine *> engines,
                                                                                         IO::Astrodynamics::Propagators::Propagator &propagator,
                                                                                         std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit,
                                                                                         const IO::Astrodynamics::Time::TDB &minimumEpoch)
        : IO::Astrodynamics::Maneuvers::ManeuverBase(std::move(engines), propagator, minimumEpoch), m_targetOrbit{std::move(targetOrbit)}
{
}

void IO::Astrodynamics::Maneuvers::OrbitalPlaneChangingManeuver::Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    auto currentStateVector = orbitalParams.ToStateVector();

    const IO::Astrodynamics::Math::Vector3D &vel = currentStateVector.GetVelocity();
    const IO::Astrodynamics::Math::Vector3D &pos = currentStateVector.GetPosition();

    //Project vector
    IO::Astrodynamics::Math::Vector3D projectedVector = vel - (pos * (vel.DotProduct(pos) / pos.DotProduct(pos)));

    //Compute relative inclination
    m_relativeInclination = std::acos(std::cos(orbitalParams.GetInclination()) * std::cos(m_targetOrbit->GetInclination()) +
                                      (std::sin(orbitalParams.GetInclination()) * std::sin(m_targetOrbit->GetInclination())) *
                                      std::cos(m_targetOrbit->GetRightAscendingNodeLongitude() - orbitalParams.GetRightAscendingNodeLongitude()));

    double rotationAngle = IO::Astrodynamics::Constants::PI2 + m_relativeInclination * 0.5;

    if (m_isAscendingNode)
    {
        rotationAngle = -rotationAngle;
    }

    //Compute deltaV
    auto deltaV = 2.0 * projectedVector.Magnitude() * std::sin(m_relativeInclination * 0.5);

    //Compute the quaternion
    auto q = IO::Astrodynamics::Math::Quaternion(pos.Normalize(), rotationAngle);

    //Rotate velocity vector
    auto rotateVecor = projectedVector.Normalize().Rotate(q);

    //Compute delta V vector
    m_deltaV = std::make_unique<IO::Astrodynamics::Math::Vector3D>(rotateVecor * deltaV);
}

IO::Astrodynamics::OrbitalParameters::StateOrientation
IO::Astrodynamics::Maneuvers::OrbitalPlaneChangingManeuver::ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint)
{
    IO::Astrodynamics::Math::Vector3D targetVector{maneuverPoint.GetSpecificAngularMomentum().Normalize()};
    if (m_isAscendingNode)
    {
        targetVector = targetVector.Reverse();
    }

    return IO::Astrodynamics::OrbitalParameters::StateOrientation{targetVector.To(m_spacecraft.Front), IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 0.0), maneuverPoint.GetEpoch(),
                                                                  maneuverPoint.GetFrame()};
}

double IO::Astrodynamics::Maneuvers::OrbitalPlaneChangingManeuver::GetRelativeInclination() const
{
    return m_relativeInclination;
}

IO::Astrodynamics::Math::Vector3D
IO::Astrodynamics::Maneuvers::OrbitalPlaneChangingManeuver::ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams)
{
    auto spacecraftSv = orbitalParams.ToStateVector();
    auto AnVector = m_targetOrbit->GetSpecificAngularMomentum().CrossProduct(orbitalParams.GetSpecificAngularMomentum()).Normalize();

    if(spacecraftSv.GetPosition().GetAngle(AnVector,spacecraftSv.GetSpecificAngularMomentum())>0.0)
    {
        m_isAscendingNode= true;
        return AnVector;
    }

    auto DnVector = AnVector.Reverse();
    m_isAscendingNode= false;
    return DnVector;
}

