/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <OblatenessPerturbation.h>
#include <Spacecraft.h>
#include "InertialFrames.h"
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(OblatenessPerturbation, ApplyToBody)
{
    IO::Astrodynamics::Integrators::Forces::OblatenessPerturbation oblatenessPerturbation;
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         6800000.0,
                                                                                                                                                                         0.0, 0.0),
                                                                                                                                                                 IO::Astrodynamics::Math::Vector3D(
                                                                                                                                                                         0.0,
                                                                                                                                                                         8000.0,
                                                                                                                                                                         0.0),
                                                                                                                                                                 IO::Astrodynamics::Time::TDB(
                                                                                                                                                                         100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));

    auto force = oblatenessPerturbation.Apply(spc, *dynamic_cast<IO::Astrodynamics::OrbitalParameters::StateVector *>(spc.GetOrbitalParametersAtEpoch().get()));
    ASSERT_EQ(IO::Astrodynamics::Math::Vector3D(-12.315596455307988, -1.7042493580662132e-08, 0.00062047268316692532), force);
}