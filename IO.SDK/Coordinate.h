/**
 * @file Coordinate.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-07
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef COORDINATE_H
#define COORDINATE_H

#include <string>

namespace IO::SDK
{
    class Coordinate
    {
    private:
        const std::string m_name;

        static Coordinate _X;
        static Coordinate _Y;
        static Coordinate _Z;
        static Coordinate _Longitude;
        static Coordinate _Latitude;
        static Coordinate _Radius;
        static Coordinate _Range;
        static Coordinate _RightAscension;
        static Coordinate _Declination;
        static Coordinate _Colatitude;
        static Coordinate _Altitude;

    public:

        /**
         * @brief Construct a new Coordinate object
         * 
         * @param name 
         */
        Coordinate(const std::string &name);

        

        /**
         * @brief Get char array
         * 
         * @return const char* 
         */
        const char *ToCharArray() const;

        static Coordinate& Altitude();
        static Coordinate& X();
        static Coordinate& Y();
        static Coordinate& Z();
        static Coordinate& Longitude();
        static Coordinate& Latitude();
        static Coordinate& Radius();
        static Coordinate& Range();
        static Coordinate& RightAscension();
        static Coordinate& Declination();
        static Coordinate& Colatitude();
    };

} // namespace IO::SDK

#endif