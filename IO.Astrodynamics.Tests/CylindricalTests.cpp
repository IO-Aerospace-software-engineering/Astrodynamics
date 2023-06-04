#include<gtest/gtest.h>
#include<Cylindrical.h>

TEST(Cylindrical, Initialization)
{
	IO::SDK::Coordinates::Cylindrical cyl(1.0, 2.0, 3.0);
	ASSERT_EQ(1.0, cyl.GetRadius());
	ASSERT_EQ(2.0, cyl.GetLongitude());
	ASSERT_EQ(3.0, cyl.GetZ());
}
