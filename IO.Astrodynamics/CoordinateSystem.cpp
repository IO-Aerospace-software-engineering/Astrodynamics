/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <CoordinateSystem.h>

#include <SDKException.h>

IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::mRectangular(std::string("RECTANGULAR"));
IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::mLatitudinal(std::string("LATITUDINAL"));
IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::mRA_DEC(std::string("RA/DEC"));
IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::mSpherical(std::string("SPHERICAL"));
IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::mCylindrical(std::string("CYLINDRICAL"));
IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::mGeodetic(std::string("GEODETIC"));
IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::mPlanetographic(std::string("PLANETOGRAPHIC"));

IO::Astrodynamics::CoordinateSystem::CoordinateSystem(std::string name) : m_name{std::move(name)}
{
}

const char *IO::Astrodynamics::CoordinateSystem::ToCharArray() const
{
    return m_name.c_str();
}

IO::Astrodynamics::CoordinateSystem &IO::Astrodynamics::CoordinateSystem::Rectangular()
{
    return mRectangular;
}
IO::Astrodynamics::CoordinateSystem &IO::Astrodynamics::CoordinateSystem::Latitudinal()
{
    return mLatitudinal;
}
IO::Astrodynamics::CoordinateSystem &IO::Astrodynamics::CoordinateSystem::RA_DEC()
{
    return mRA_DEC;
}
IO::Astrodynamics::CoordinateSystem &IO::Astrodynamics::CoordinateSystem::Spherical()
{
    return mSpherical;
}
IO::Astrodynamics::CoordinateSystem &IO::Astrodynamics::CoordinateSystem::Cylindrical()
{
    return mCylindrical;
}
IO::Astrodynamics::CoordinateSystem &IO::Astrodynamics::CoordinateSystem::Geodetic()
{
    return mGeodetic;
}
IO::Astrodynamics::CoordinateSystem &IO::Astrodynamics::CoordinateSystem::Planetographic()
{
    return mPlanetographic;
}

IO::Astrodynamics::CoordinateSystem IO::Astrodynamics::CoordinateSystem::ToCoordinateSystemType(const std::string &coordinateSystemType)
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

    throw IO::Astrodynamics::Exception::SDKException("Invalid coordinate system type : " + coordinateSystemType);
}
