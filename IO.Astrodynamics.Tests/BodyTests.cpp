#include <gtest/gtest.h>
#include <StateVector.h>
#include <Constants.h>
using namespace std::chrono_literals;
TEST(Body, Initialization)
{
	auto sun=std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
	IO::Astrodynamics::Body::CelestialBody body(399, sun);
	ASSERT_EQ(399, body.GetId());
	ASSERT_STREQ("EARTH", body.GetName().c_str());
	ASSERT_DOUBLE_EQ(5.9721683987248994e+24, body.GetMass());
	ASSERT_DOUBLE_EQ(5.9721683987248994e+24 * IO::Astrodynamics::Constants::G, body.GetMu());
}

TEST(Body, Satellites)
{
	auto sun=std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
	auto earth=std::make_shared<IO::Astrodynamics::Body::CelestialBody>(3, sun);

	//Fake data, just for technical validation
	ASSERT_EQ(0, earth->GetSatellites().size());
	ASSERT_EQ(1, sun->GetSatellites().size());
	ASSERT_EQ(*earth,*sun->GetSatellites()[0]);
}

TEST(Body, SubObserverPoint)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);

    auto subpoint = moon->GetSubObserverPoint(*earth, IO::Astrodynamics::AberrationsEnum::LT, IO::Astrodynamics::Time::TDB("2021-06-28T00:00:00"));
    ASSERT_DOUBLE_EQ(0.83233741162176433, subpoint.GetLongitude());
    ASSERT_DOUBLE_EQ(-0.34238142277532951, subpoint.GetLatitude());
    ASSERT_DOUBLE_EQ(1.0586118481814565e-12, subpoint.GetAltitude());
}

TEST(Body, SubSolarPoint)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);

    auto subpoint = moon->GetSubSolarPoint(*earth, IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB("2021-06-28T12:00:00"));
    ASSERT_DOUBLE_EQ(0.01592876989506849, subpoint.GetLongitude());
    ASSERT_DOUBLE_EQ(0.40823584501112109, subpoint.GetLatitude());
    ASSERT_DOUBLE_EQ(0.0, subpoint.GetAltitude());
}