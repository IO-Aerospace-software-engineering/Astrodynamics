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
#include "SDKException.h"

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

IO::SDK::Coordinate IO::SDK::Coordinate::ToCoordinateType(const std::string &coordinateType)
{
    if (coordinateType == Coordinate::mAltitude.ToCharArray())
    {
        return mAltitude;
    } else if (coordinateType == Coordinate::mX.ToCharArray())
    {
        return mX;
    } else if (coordinateType == Coordinate::mY.ToCharArray())
    {
        return mY;
    } else if (coordinateType == Coordinate::mZ.ToCharArray())
    {
        return mZ;
    } else if (coordinateType == Coordinate::mLongitude.ToCharArray())
    {
        return mLongitude;
    } else if (coordinateType == Coordinate::mLatitude.ToCharArray())
    {
        return mLatitude;
    } else if (coordinateType == Coordinate::mRadius.ToCharArray())
    {
        return mRadius;
    } else if (coordinateType == Coordinate::mRange.ToCharArray())
    {
        return mRange;
    } else if (coordinateType == Coordinate::mRightAscension.ToCharArray())
    {
        return mRightAscension;
    } else if (coordinateType == Coordinate::mDeclination.ToCharArray())
    {
        return mDeclination;
    } else if (coordinateType == Coordinate::mColatitude.ToCharArray())
    {
        return mColatitude;
    }

    throw IO::SDK::Exception::SDKException("Invalid coordinate type : " + coordinateType);
}
