//
// Created by s.guillet on 17/07/2023.
//

#include "LagrangePoint.h"
#include "CelestialBody.h"
#include "StateVector.h"
#include "InertialFrames.h"

using namespace std::chrono_literals;

IO::Astrodynamics::Body::LagrangePoint::LagrangePoint(int id, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> centerOfMotion) : CelestialItem(id, "L" +
                                                                                                                                                          std::to_string(id % 390),
                                                                                                                                                      0.0, centerOfMotion)
{
}

