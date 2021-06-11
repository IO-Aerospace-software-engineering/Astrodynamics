#include<gtest/gtest.h>
#include<Vector3D.h>
#include<SurfaceInterceptPoint.h>
#include<TDB.h>

using namespace std::chrono_literals;
TEST(SurfaceInterceptpoint, Initialization)
{
	IO::SDK::Math::Vector3D v1(1.0, 2.0, 3.0);
	IO::SDK::Math::Vector3D v2(4.0, 5.0, 6.0);
	IO::SDK::Time::TDB epoch{ 123.5s };
	IO::SDK::Coordinates::SurfaceInterceptPoint sf(v1, v2, epoch);
	ASSERT_EQ(v1.GetX(), sf.GetInterceptPoint().GetX());
	ASSERT_EQ(v1.GetY(), sf.GetInterceptPoint().GetY());
	ASSERT_EQ(v1.GetZ(), sf.GetInterceptPoint().GetZ());

	ASSERT_EQ(v2.GetX(), sf.GetObserverInterceptPointVector().GetX());
	ASSERT_EQ(v2.GetY(), sf.GetObserverInterceptPointVector().GetY());
	ASSERT_EQ(v2.GetZ(), sf.GetObserverInterceptPointVector().GetZ());

	ASSERT_EQ(epoch.GetSecondsFromJ2000().count(), sf.GetInterceptEpoch().GetSecondsFromJ2000().count());
}

TEST(SurfaceInterceptpoint, Copy)
{
	IO::SDK::Math::Vector3D v1(1.0, 2.0, 3.0);
	IO::SDK::Math::Vector3D v2(4.0, 5.0, 6.0);
	IO::SDK::Time::TDB epoch{ 123.5s };
	IO::SDK::Coordinates::SurfaceInterceptPoint sf(v1, v2, epoch);
	IO::SDK::Coordinates::SurfaceInterceptPoint sfCopy(sf);

	ASSERT_EQ(v1.GetX(), sfCopy.GetInterceptPoint().GetX());
	ASSERT_EQ(v1.GetY(), sfCopy.GetInterceptPoint().GetY());
	ASSERT_EQ(v1.GetZ(), sfCopy.GetInterceptPoint().GetZ());

	ASSERT_EQ(v2.GetX(), sfCopy.GetObserverInterceptPointVector().GetX());
	ASSERT_EQ(v2.GetY(), sfCopy.GetObserverInterceptPointVector().GetY());
	ASSERT_EQ(v2.GetZ(), sfCopy.GetObserverInterceptPointVector().GetZ());

	ASSERT_EQ(epoch.GetSecondsFromJ2000().count(), sfCopy.GetInterceptEpoch().GetSecondsFromJ2000().count());

	ASSERT_NE(&(sf.GetInterceptPoint()), &(sfCopy.GetInterceptPoint()));
	ASSERT_NE(&(sf.GetObserverInterceptPointVector()), &(sfCopy.GetObserverInterceptPointVector()));
}