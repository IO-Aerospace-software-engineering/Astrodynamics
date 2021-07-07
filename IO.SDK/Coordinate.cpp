/**
 * @file Coordinate.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Coordinate.h>

IO::SDK::Coordinate IO::SDK::Coordinate::_X(std::string("X"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Y(std::string("Y"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Z(std::string("Z"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Altitude(std::string("ALTITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Colatitude(std::string("COLATITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Declination(std::string("DECLINATION"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Latitude(std::string("LATITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Longitude(std::string("LONGITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Radius(std::string("RADIUS"));
IO::SDK::Coordinate IO::SDK::Coordinate::_Range(std::string("RANGE"));
IO::SDK::Coordinate IO::SDK::Coordinate::_RightAscension(std::string("RIGHT ASCENSION"));

IO::SDK::Coordinate::Coordinate(const std::string &name) : m_name{name}
{
}

const char *IO::SDK::Coordinate::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Altitude()
{
    return _Altitude;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::X()
{
    return _X;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Y()
{
    return _Y;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Z()
{
    return _Z;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Longitude()
{
    return _Longitude;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Latitude()
{
    return _Latitude;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Radius()
{
    return _Radius;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Range()
{
    return _Range;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::RightAscension()
{
    return _RightAscension;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Declination()
{
    return _Declination;
}

IO::SDK::Coordinate &IO::SDK::Coordinate::Colatitude()
{
    return _Colatitude;
}