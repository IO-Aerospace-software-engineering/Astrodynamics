/**
 * @file CoordinateSystem.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-07
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef COORDINATE_SYSTEM_H
#define COORDINATE_SYSTEM_H

#include <string>

namespace IO::SDK
{
    class CoordinateSystem
    {
    private:
        const std::string m_name;
        static CoordinateSystem _Rectangular;
        static CoordinateSystem _Latitudinal;
        static CoordinateSystem _RA_DEC;
        static CoordinateSystem _Spherical;
        static CoordinateSystem _Cylindrical;
        static CoordinateSystem _Geodetic;
        static CoordinateSystem _Planetographic;

    public:
        /**
         * @brief Construct a new Coordinate System object
         * 
         * @param name 
         */
        CoordinateSystem(const std::string &name);

        /**
         * @brief Get char array coordinate system
         * 
         * @return const char* 
         */
        const char *ToCharArray() const;

        static CoordinateSystem& Rectangular();
        static CoordinateSystem& Latitudinal();
        static CoordinateSystem& RA_DEC();
        static CoordinateSystem& Spherical();
        static CoordinateSystem& Cylindrical();
        static CoordinateSystem& Geodetic();
        static CoordinateSystem& Planetographic();
    };

} // namespace IO::SDK

#endif