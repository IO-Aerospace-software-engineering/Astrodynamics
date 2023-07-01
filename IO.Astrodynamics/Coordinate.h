/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef COORDINATE_H
#define COORDINATE_H

#include <string>

namespace IO::Astrodynamics
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
        explicit Coordinate(std::string name);
        ~Coordinate() = default;

        Coordinate &operator=(const Coordinate &other)
        {
            if (this == &other)
                return *this;

            const_cast<std::string &>(m_name) = other.m_name;
            return *this;
        }
        

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
        static Coordinate& ToCoordinateType(const std::string &coordinateType) ;
    };

} // namespace IO::Astrodynamics

#endif