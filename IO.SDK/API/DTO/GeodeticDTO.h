//
// Created by spacer on 3/23/23.
//

#ifndef IOSDK_GEODETICDTO_H
#define IOSDK_GEODETICDTO_H

namespace IO::SDK::API::DTO
{
    struct GeodeticDTO
    {
        GeodeticDTO() : longitude{0.0}, latitude{0.0}, altitude{0.0}
        {}

        GeodeticDTO(double longitude, double latitude, double altitude) : longitude(longitude), latitude(latitude), altitude(altitude)
        {}

        double longitude, latitude, altitude;
    };
}

#endif //IOSDK_GEODETICDTO_H
