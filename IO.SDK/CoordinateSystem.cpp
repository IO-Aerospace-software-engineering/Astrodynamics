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

IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::_Rectangular(std::string("RECTANGULAR"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::_Latitudinal(std::string("LATITUDINAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::_RA_DEC(std::string("RA/DEC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::_Spherical(std::string("SPHERICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::_Cylindrical(std::string("CYLINDRICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::_Geodetic(std::string("GEODETIC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::_Planetographic(std::string("PLANETOGRAPHIC"));

IO::SDK::CoordinateSystem::CoordinateSystem(const std::string &name) : m_name{name}
{
}

const char *IO::SDK::CoordinateSystem::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Rectangular()
{
    return _Rectangular;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Latitudinal()
{
    return _Latitudinal;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::RA_DEC()
{
    return _RA_DEC;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Spherical()
{
    return _Spherical;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Cylindrical()
{
    return _Cylindrical;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Geodetic()
{
    return _Geodetic;
}
IO::SDK::CoordinateSystem &IO::SDK::CoordinateSystem::Planetographic()
{
    return _Planetographic;
}