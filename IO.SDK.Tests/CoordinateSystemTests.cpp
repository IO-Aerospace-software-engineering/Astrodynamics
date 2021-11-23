#include <gtest/gtest.h>
#include <CoordinateSystem.h>

TEST(CoordinateSystem, ToCharArray)
{
    ASSERT_STREQ("PLANETOGRAPHIC", IO::SDK::CoordinateSystem::Planetographic().ToCharArray());
    ASSERT_STREQ("CYLINDRICAL", IO::SDK::CoordinateSystem::Cylindrical().ToCharArray());
    ASSERT_STREQ("GEODETIC", IO::SDK::CoordinateSystem::Geodetic().ToCharArray());
    ASSERT_STREQ("LATITUDINAL", IO::SDK::CoordinateSystem::Latitudinal().ToCharArray());
    ASSERT_STREQ("PLANETOGRAPHIC", IO::SDK::CoordinateSystem::Planetographic().ToCharArray());
    ASSERT_STREQ("RA/DEC", IO::SDK::CoordinateSystem::RA_DEC().ToCharArray());
    ASSERT_STREQ("RECTANGULAR", IO::SDK::CoordinateSystem::Rectangular().ToCharArray());
    ASSERT_STREQ("SPHERICAL", IO::SDK::CoordinateSystem::Spherical().ToCharArray());
}