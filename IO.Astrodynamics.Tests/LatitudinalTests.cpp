#include<gtest/gtest.h>
#include<Latitudinal.h>

TEST(Latitudinal, Initialization)
{
	IO::Astrodynamics::Coordinates::Latitudinal lat(1.0, 2.0, 3.0);
	ASSERT_EQ(1.0, lat.GetLongitude());
	ASSERT_EQ(2.0, lat.GetLatitude());
	ASSERT_EQ(3.0, lat.GetRadius());
}
