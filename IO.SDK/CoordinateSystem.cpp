/**
 * @file CoordinateSystem.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <CoordinateSystem.h>

IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mRectangular(std::string("RECTANGULAR"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mLatitudinal(std::string("LATITUDINAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mRA_DEC(std::string("RA/DEC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mSpherical(std::string("SPHERICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mCylindrical(std::string("CYLINDRICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mGeodetic(std::string("GEODETIC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mPlanetographic(std::string("PLANETOGRAPHIC"));

IO::SDK::CoordinateSystem::CoordinateSystem(const std::string &name) : m_name{name}
{
}

const char *IO::SDK::CoordinateSystem::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Rectangular()
{
    return mRectangular;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Latitudinal()
{
    return mLatitudinal;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::RA_DEC()
{
    return mRA_DEC;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Spherical()
{
    return mSpherical;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Cylindrical()
{
    return mCylindrical;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Geodetic()
{
    return mGeodetic;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Planetographic()
{
    return mPlanetographic;
}