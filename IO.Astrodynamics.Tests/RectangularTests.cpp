#include<gtest/gtest.h>
#include<Rectangular.h>

TEST(Rectangular, Initialization)
{
	IO::SDK::Coordinates::Rectangular rec(1.0, 2.0, 3.0);
	ASSERT_EQ(1.0, rec.GetX());
	ASSERT_EQ(2.0, rec.GetY());
	ASSERT_EQ(3.0, rec.GetZ());
}
