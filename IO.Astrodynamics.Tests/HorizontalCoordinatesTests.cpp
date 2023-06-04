#include <gtest/gtest.h>
#include <HorizontalCoordinates.h>

TEST(HorizontalCoordinates, Initialization)
{
    IO::Astrodynamics::Coordinates::HorizontalCoordinates hor{1.0, 2.0, 3.0};
    ASSERT_DOUBLE_EQ(1.0, hor.GetAzimuth());
    ASSERT_DOUBLE_EQ(2.0, hor.GetElevation());
    ASSERT_DOUBLE_EQ(3.0, hor.GetAltitude());
}