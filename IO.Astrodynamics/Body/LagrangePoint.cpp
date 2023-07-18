//
// Created by s.guillet on 17/07/2023.
//

#include "LagrangePoint.h"
#include "CelestialBody.h"
#include "StateVector.h"
#include "InertialFrames.h"
#include "Proxy.h"
#include "Converters.cpp"

using namespace std::chrono_literals;

IO::Astrodynamics::Body::LagrangePoint::LagrangePoint(int id) : CelestialItem(id, "L" + std::to_string(id % 390), 0.0)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto res = ReadEphemerisAtGivenEpochProxy(0.0, earth->GetId(), id, IO::Astrodynamics::Frames::InertialFrames::Ecliptic().ToCharArray(), "NONE");
    m_orbitalParametersAtEpoch = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, ToVector3D(res.position), ToVector3D(res.velocity),
                                                                                                     IO::Astrodynamics::Time::TDB(0s),
                                                                                                     IO::Astrodynamics::Frames::InertialFrames::Ecliptic());
}

