#include<gtest/gtest.h>
#include<Illumination.h>
#include<IlluminationAngle.h>
#include<Vector3D.h>
#include<TDB.h>
#include<functional>

using namespace std::chrono_literals;
TEST(Illumination, Initialization)
{
	IO::SDK::Math::Vector3D v(1.0, 2.0, 3.0);
	IO::SDK::Time::TDB tdb(789.3s);
	IO::SDK::Illumination::Illumination ill(v, 5.0, 6.0, 7.0, tdb);
	ASSERT_EQ(1.0, ill.GetObserverToSurfacePoint().GetX());
	ASSERT_EQ(2.0, ill.GetObserverToSurfacePoint().GetY());
	ASSERT_EQ(3.0, ill.GetObserverToSurfacePoint().GetZ());
	ASSERT_EQ(5.0, ill.GetPhaseAngle());
	ASSERT_EQ(6.0, ill.GetIncidence());
	ASSERT_EQ(7.0, ill.GetEmission());
	ASSERT_EQ(789.3, ill.GetEpoch().GetSecondsFromJ2000().count());
}

TEST(Illumination, Copy)
{
	IO::SDK::Time::TDB tdb(789.3s);
	IO::SDK::Math::Vector3D v(1.0, 2.0, 3.0);
	IO::SDK::Illumination::Illumination ill(v, 5.0, 6.0, 7.0, tdb);
	IO::SDK::Illumination::Illumination illCopy = ill;

	ASSERT_NE(&(ill.GetObserverToSurfacePoint()), &(illCopy.GetObserverToSurfacePoint()));
	ASSERT_EQ(1.0, illCopy.GetObserverToSurfacePoint().GetX());
	ASSERT_EQ(2.0, illCopy.GetObserverToSurfacePoint().GetY());
	ASSERT_EQ(3.0, illCopy.GetObserverToSurfacePoint().GetZ());
	ASSERT_EQ(5.0, illCopy.GetPhaseAngle());
	ASSERT_EQ(6.0, illCopy.GetIncidence());
	ASSERT_EQ(7.0, illCopy.GetEmission());
	ASSERT_EQ(789.3, illCopy.GetEpoch().GetSecondsFromJ2000().count());
}

TEST(Illumination, Types)
{
	ASSERT_STREQ("EMISSION", IO::SDK::IlluminationAngle::Emission().ToCharArray());
	ASSERT_STREQ("PHASE", IO::SDK::IlluminationAngle::Phase().ToCharArray());
	ASSERT_STREQ("INCIDENCE", IO::SDK::IlluminationAngle::Incidence().ToCharArray());
}