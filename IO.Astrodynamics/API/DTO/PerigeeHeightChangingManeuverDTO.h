/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 3/27/23.
//

#ifndef IOSDK_PERIGEEHEIGHTCHANGINGMANEUVERDTO_H
#define IOSDK_PERIGEEHEIGHTCHANGINGMANEUVERDTO_H
#include <WindowDTO.h>
#include <Vector3DDTO.h>
namespace IO::Astrodynamics::API::DTO
{
    struct PerigeeHeightChangingManeuverDTO
    {
        int maneuverOrder{-1};
        char* engines[5]{};
        double attitudeHoldDuration{0.0};
        double minimumEpoch{0.0};

        double targetHeight{0.0};

        IO::Astrodynamics::API::DTO::WindowDTO maneuverWindow{};
        IO::Astrodynamics::API::DTO::WindowDTO thrustWindow{};
        IO::Astrodynamics::API::DTO::WindowDTO attitudeWindow{};
        IO::Astrodynamics::API::DTO::Vector3DDTO deltaV{};
        double FuelBurned{0.0};
    };
}

#endif //IOSDK_PERIGEEHEIGHTCHANGINGMANEUVERDTO_H