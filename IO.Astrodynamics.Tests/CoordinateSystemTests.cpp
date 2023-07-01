#include <gtest/gtest.h>
#include <CoordinateSystem.h>

TEST(CoordinateSystem, ToCharArray)
{
    ASSERT_STREQ("PLANETOGRAPHIC", IO::Astrodynamics::CoordinateSystem::Planetographic().ToCharArray());
    ASSERT_STREQ("CYLINDRICAL", IO::Astrodynamics::CoordinateSystem::Cylindrical().ToCharArray());
    ASSERT_STREQ("GEODETIC", IO::Astrodynamics::CoordinateSystem::Geodetic().ToCharArray());
    ASSERT_STREQ("LATITUDINAL", IO::Astrodynamics::CoordinateSystem::Latitudinal().ToCharArray());
    ASSERT_STREQ("PLANETOGRAPHIC", IO::Astrodynamics::CoordinateSystem::Planetographic().ToCharArray());
    ASSERT_STREQ("RA/DEC", IO::Astrodynamics::CoordinateSystem::RA_DEC().ToCharArray());
    ASSERT_STREQ("RECTANGULAR", IO::Astrodynamics::CoordinateSystem::Rectangular().ToCharArray());
    ASSERT_STREQ("SPHERICAL", IO::Astrodynamics::CoordinateSystem::Spherical().ToCharArray());
}