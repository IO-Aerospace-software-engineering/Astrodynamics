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
#include<Coordinate.h>

IO::SDK::Coordinate IO::SDK::Coordinate::X(std::string("X"));
IO::SDK::Coordinate IO::SDK::Coordinate::Y(std::string("Y"));
IO::SDK::Coordinate IO::SDK::Coordinate::Z(std::string("Z"));
IO::SDK::Coordinate IO::SDK::Coordinate::Altitude(std::string("ALTITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::Colatitude(std::string("COLATITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::Declination(std::string("DECLINATION"));
IO::SDK::Coordinate IO::SDK::Coordinate::Latitude(std::string("LATITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::Longitude(std::string("LONGITUDE"));
IO::SDK::Coordinate IO::SDK::Coordinate::Radius(std::string("RADIUS"));
IO::SDK::Coordinate IO::SDK::Coordinate::Range(std::string("RANGE"));
IO::SDK::Coordinate IO::SDK::Coordinate::RightAscension(std::string("RIGHT ASCENSION"));

IO::SDK::Coordinate::Coordinate(const std::string &name) : m_name{name}
{
}

const char *IO::SDK::Coordinate::ToCharArray() const
{
    return m_name.c_str();
}