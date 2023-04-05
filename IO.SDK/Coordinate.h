/**
 * @file Coordinate.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
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

        static Coordinate mX;
        static Coordinate mY;
        static Coordinate mZ;
        static Coordinate mLongitude;
        static Coordinate mLatitude;
        static Coordinate mRadius;
        static Coordinate mRange;
        static Coordinate mRightAscension;
        static Coordinate mDeclination;
        static Coordinate mColatitude;
        static Coordinate mAltitude;

    public:

        /**
         * @brief Construct a new Coordinate object
         * 
         * @param name 
         */
        explicit Coordinate(const std::string &name);

        

        /**
         * @brief Get char array
         * 
         * @return const char* 
         */
        [[nodiscard]] const char *ToCharArray() const;

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