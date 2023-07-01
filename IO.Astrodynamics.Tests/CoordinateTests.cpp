#include <gtest/gtest.h>
#include <Coordinate.h>

TEST(Coordinate, ToCharArray)
{

    ASSERT_STREQ("ALTITUDE", IO::Astrodynamics::Coordinate::Altitude().ToCharArray());
    ASSERT_STREQ("COLATITUDE", IO::Astrodynamics::Coordinate::Colatitude().ToCharArray());
    ASSERT_STREQ("DECLINATION", IO::Astrodynamics::Coordinate::Declination().ToCharArray());
    ASSERT_STREQ("LATITUDE", IO::Astrodynamics::Coordinate::Latitude().ToCharArray());
    ASSERT_STREQ("LONGITUDE", IO::Astrodynamics::Coordinate::Longitude().ToCharArray());
    ASSERT_STREQ("RADIUS", IO::Astrodynamics::Coordinate::Radius().ToCharArray());
    ASSERT_STREQ("RANGE", IO::Astrodynamics::Coordinate::Range().ToCharArray());
    ASSERT_STREQ("RIGHT ASCENSION", IO::Astrodynamics::Coordinate::RightAscension().ToCharArray());
    ASSERT_STREQ("X", IO::Astrodynamics::Coordinate::X().ToCharArray());
    ASSERT_STREQ("Y", IO::Astrodynamics::Coordinate::Y().ToCharArray());
    ASSERT_STREQ("Z", IO::Astrodynamics::Coordinate::Z().ToCharArray());
}