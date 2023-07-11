/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef IOSDK_GEODETICDTO_H
#define IOSDK_GEODETICDTO_H

namespace IO::Astrodynamics::API::DTO
{
    struct PlanetodeticDTO
    {
        PlanetodeticDTO() : longitude{0.0}, latitude{0.0}, altitude{0.0}
        {}

        PlanetodeticDTO(double longitude, double latitude, double altitude) : longitude(longitude), latitude(latitude), altitude(altitude)
        {}

        double longitude, latitude, altitude;
    };
}

#endif //IOSDK_GEODETICDTO_H
