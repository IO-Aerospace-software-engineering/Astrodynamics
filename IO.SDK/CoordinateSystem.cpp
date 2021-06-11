#include <CoordinateSystem.h>

IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::Rectangular(std::string("RECTANGULAR"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::Latitudinal(std::string("LATITUDINAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::RA_DEC(std::string("RA/DEC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::Spherical(std::string("SPHERICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::Cylindrical(std::string("CYLINDRICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::Geodetic(std::string("GEODETIC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::Planetographic(std::string("PLANETOGRAPHIC"));

IO::SDK::CoordinateSystem::CoordinateSystem(const std::string &name) : m_name{name}
{
}

const char *IO::SDK::CoordinateSystem::ToCharArray() const
{
    return m_name.c_str();
}