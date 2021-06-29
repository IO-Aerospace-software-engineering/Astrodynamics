#include <gtest/gtest.h>
#include <CelestialBody.h>
#include <StateVector.h>
#include <TDB.h>
#include <chrono>
#include <memory>
using namespace std::chrono_literals;
TEST(Body, Initialization)
{
	auto sun=std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
	IO::SDK::Body::CelestialBody body(399, "earth", sun);
	ASSERT_EQ(399, body.GetId());
	ASSERT_STREQ("earth", body.GetName().c_str());
	ASSERT_DOUBLE_EQ(5.9721683987248994e+24, body.GetMass());
	ASSERT_DOUBLE_EQ(5.9721683987248994e+24 * IO::SDK::Constants::G, body.GetMu());
}

TEST(Body, Satellites)
{
	auto sun=std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
	auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(3, "earth", sun);

	//Fake data, just for technical validation
	ASSERT_EQ(0, earth->GetSatellites().size());
	ASSERT_EQ(1, sun->GetSatellites().size());
	ASSERT_EQ(*earth,*sun->GetSatellites()[0]);
}

TEST(Body, SubObserverPoint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);

    auto subpoint = moon->GetSubObserverPoint(*earth, IO::SDK::AberrationsEnum::LT, IO::SDK::Time::TDB("2021-06-28T00:00:00"));
    ASSERT_DOUBLE_EQ(0.83233741162176433, subpoint.GetLongitude());
    ASSERT_DOUBLE_EQ(-0.34238142277532951, subpoint.GetLatitude());
    ASSERT_DOUBLE_EQ(1.0586118481814565e-12, subpoint.GetAltitude());
}