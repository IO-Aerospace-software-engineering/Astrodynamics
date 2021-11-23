#include <gtest/gtest.h>
#include <Coordinate.h>

TEST(Coordinate, ToCharArray)
{

    ASSERT_STREQ("ALTITUDE", IO::SDK::Coordinate::Altitude().ToCharArray());
    ASSERT_STREQ("COLATITUDE", IO::SDK::Coordinate::Colatitude().ToCharArray());
    ASSERT_STREQ("DECLINATION", IO::SDK::Coordinate::Declination().ToCharArray());
    ASSERT_STREQ("LATITUDE", IO::SDK::Coordinate::Latitude().ToCharArray());
    ASSERT_STREQ("LONGITUDE", IO::SDK::Coordinate::Longitude().ToCharArray());
    ASSERT_STREQ("RADIUS", IO::SDK::Coordinate::Radius().ToCharArray());
    ASSERT_STREQ("RANGE", IO::SDK::Coordinate::Range().ToCharArray());
    ASSERT_STREQ("RIGHT ASCENSION", IO::SDK::Coordinate::RightAscension().ToCharArray());
    ASSERT_STREQ("X", IO::SDK::Coordinate::X().ToCharArray());
    ASSERT_STREQ("Y", IO::SDK::Coordinate::Y().ToCharArray());
    ASSERT_STREQ("Z", IO::SDK::Coordinate::Z().ToCharArray());
}