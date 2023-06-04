/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <CoordinateSystem.h>

#include <SDKException.h>

IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mRectangular(std::string("RECTANGULAR"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mLatitudinal(std::string("LATITUDINAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mRA_DEC(std::string("RA/DEC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mSpherical(std::string("SPHERICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mCylindrical(std::string("CYLINDRICAL"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mGeodetic(std::string("GEODETIC"));
IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::mPlanetographic(std::string("PLANETOGRAPHIC"));

IO::SDK::CoordinateSystem::CoordinateSystem(std::string name) : m_name{std::move(name)}
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

IO::SDK::CoordinateSystem IO::SDK::CoordinateSystem::ToCoordinateSystemType(const std::string &coordinateSystemType)
{
    if (coordinateSystemType == CoordinateSystem::mCylindrical.ToCharArray())
    {
        return mCylindrical;
    } else if (coordinateSystemType == CoordinateSystem::mGeodetic.ToCharArray())
    {
        return mGeodetic;
    } else if (coordinateSystemType == CoordinateSystem::mLatitudinal.ToCharArray())
    {
        return mLatitudinal;
    } else if (coordinateSystemType == CoordinateSystem::mRectangular.ToCharArray())
    {
        return mRectangular;
    } else if (coordinateSystemType == CoordinateSystem::mPlanetographic.ToCharArray())
    {
        return mPlanetographic;
    } else if (coordinateSystemType == CoordinateSystem::mSpherical.ToCharArray())
    {
        return mSpherical;
    } else if (coordinateSystemType == CoordinateSystem::RA_DEC().ToCharArray())
    {
        return mRA_DEC;
    }

    throw IO::SDK::Exception::SDKException("Invalid coordinate system type : " + coordinateSystemType);
}
