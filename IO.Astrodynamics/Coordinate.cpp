/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Coordinate.h>

#include <utility>
#include <SDKException.h>

IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mX(std::string("X"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mY(std::string("Y"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mZ(std::string("Z"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mAltitude(std::string("ALTITUDE"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mColatitude(std::string("COLATITUDE"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mDeclination(std::string("DECLINATION"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mLatitude(std::string("LATITUDE"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mLongitude(std::string("LONGITUDE"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mRadius(std::string("RADIUS"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mRange(std::string("RANGE"));
IO::Astrodynamics::Coordinate IO::Astrodynamics::Coordinate::mRightAscension(std::string("RIGHT ASCENSION"));

IO::Astrodynamics::Coordinate::Coordinate(std::string name) : m_name{std::move(name)}
{
}

const char *IO::Astrodynamics::Coordinate::ToCharArray() const
{
    return m_name.c_str();
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Altitude()
{
    return mAltitude;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::X()
{
    return mX;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Y()
{
    return mY;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Z()
{
    return mZ;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Longitude()
{
    return mLongitude;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Latitude()
{
    return mLatitude;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Radius()
{
    return mRadius;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Range()
{
    return mRange;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::RightAscension()
{
    return mRightAscension;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Declination()
{
    return mDeclination;
}

IO::Astrodynamics::Coordinate &IO::Astrodynamics::Coordinate::Colatitude()
{
    return mColatitude;
}

IO::Astrodynamics::Coordinate& IO::Astrodynamics::Coordinate::ToCoordinateType(const std::string &coordinateType)
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

    throw IO::Astrodynamics::Exception::SDKException("Invalid coordinate type : " + coordinateType);
}
