#include<gtest/gtest.h>
#include<Planetographic.h>

TEST(Planetographic, Initialization)
{
	IO::SDK::Coordinates::Planetographic pla(1.0, 2.0, 3.0);

	ASSERT_EQ(1.0, pla.GetLongitude());
	ASSERT_EQ(2.0, pla.GetLatitude());
	ASSERT_EQ(3.0, pla.GetAltitude());
}
