/**
 * @file Coordinate.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Coordinate.h>

IO::SDK::Coordinate IO::SDK::Coordinate::mX(std::string("X"));
IO::SDK::Coordinate IO::SDK::Coordinate::mY(std::string("Y"));
IO::SDK::Coordinate IO::SDK::Coordinate::mZ(std::string("Z"));
IO::SDK::Coordinate IO::SDK::Coordinate::mAltitude(std::string("ALTITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::mColatitude(std::string("COLATITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::mDeclination(std::string("DECLINATION"));
IO::SDK::Coordinate IO::SDK::Coordinate::mLatitude(std::string("LATITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::mLongitude(std::string("LONGITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::mRadius(std::string("RADIUS"));
IO::SDK::Coordinate IO::SDK::Coordinate::mRange(std::string("RANGE"));
IO::SDK::Coordinate IO::SDK::Coordinate::mRightAscension(std::string("RIGHT ASCENSION"));

IO::SDK::Coordinate::Coordinate(const std::string &name) : m_name{name}
{
}

const char *IO::SDK::Coordinate::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Altitude()
{
    return mAltitude;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::X()
{
    return mX;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Y()
{
    return mY;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Z()
{
    return mZ;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Longitude()
{
    return mLongitude;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Latitude()
{
    return mLatitude;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Radius()
{
    return mRadius;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Range()
{
    return mRange;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::RightAscension()
{
    return mRightAscension;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Declination()
{
    return mDeclination;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Colatitude()
{
    return mColatitude;
}