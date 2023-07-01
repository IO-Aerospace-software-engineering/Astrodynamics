#include<gtest/gtest.h>
#include<Geodetic.h>

TEST(Geodetic, Initialization)
{
	IO::Astrodynamics::Coordinates::Geodetic geo(1.0, 2.0, 3.0);
	
	ASSERT_EQ(1.0, geo.GetLongitude());
	ASSERT_EQ(2.0, geo.GetLatitude());
	ASSERT_EQ(3.0, geo.GetAltitude());
}
