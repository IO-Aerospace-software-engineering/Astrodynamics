#include<gtest/gtest.h>
#include<Spherical.h>

TEST(Spherical, Initialization)
{
	IO::SDK::Coordinates::Spherical sph(1.0, 2.0, 3.0);
	ASSERT_EQ(1.0, sph.GetLongitude());
	ASSERT_EQ(2.0, sph.GetColatitude());
	ASSERT_EQ(3.0, sph.GetRadius());
}
