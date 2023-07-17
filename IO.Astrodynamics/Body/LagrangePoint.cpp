//
// Created by s.guillet on 17/07/2023.
//

#include "LagrangePoint.h"
#include "CelestialBody.h"

IO::Astrodynamics::Body::LagrangePoint::LagrangePoint(int id) : CelestialItem(id, "L" + std::to_string(id % 390), 0.0)
{
}

