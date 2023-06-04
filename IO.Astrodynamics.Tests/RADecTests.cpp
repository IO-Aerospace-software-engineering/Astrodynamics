#include<gtest/gtest.h>
#include<Equatorial.h>

TEST(RADec, Initialization)
{
	IO::SDK::Coordinates::Equatorial radec(1.0, 2.0, 3.0);
	ASSERT_EQ(1.0, radec.GetRA());
	ASSERT_EQ(2.0, radec.GetDec());
	ASSERT_EQ(3.0, radec.GetRange());
}
