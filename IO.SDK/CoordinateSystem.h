/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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
        static CoordinateSystem mRectangular;
        static CoordinateSystem mLatitudinal;
        static CoordinateSystem mRA_DEC;
        static CoordinateSystem mSpherical;
        static CoordinateSystem mCylindrical;
        static CoordinateSystem mGeodetic;
        static CoordinateSystem mPlanetographic;

    public:
        /**
         * @brief Construct a new Coordinate System object
         * 
         * @param name 
         */
        explicit CoordinateSystem(std::string name);

        ~CoordinateSystem() = default;

        CoordinateSystem &operator=(const CoordinateSystem &other)
        {
            if (this == &other)
                return *this;

            const_cast<std::string &>(m_name) = other.m_name;
            return *this;
        }

        /**
         * @brief Get char array coordinate system
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

        static CoordinateSystem& Rectangular();
        static CoordinateSystem& Latitudinal();
        static CoordinateSystem& RA_DEC();
        static CoordinateSystem& Spherical();
        static CoordinateSystem& Cylindrical();
        static CoordinateSystem& Geodetic();
        static CoordinateSystem& Planetographic();
        static CoordinateSystem ToCoordinateSystemType(const std::string &coordinateSystemType) ;
    };

} // namespace IO::SDK

#endif