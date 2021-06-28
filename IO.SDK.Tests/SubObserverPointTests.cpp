#include <gtest/gtest.h>
#include <memory>

#include <Planetographic.h>
#include <Body.h>
#include <CelestialBody.h>
#include <TDB.h>
#include <Aberrations.h>

TEST(SubObserverPoint, GetPlanetographicPoint)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, "moon", earth);

    auto subpoint = moon->GetSubObserverPoint(*earth, IO::SDK::AberrationsEnum::LT, IO::SDK::Time::TDB("2021-06-28T00:00:00"));
    ASSERT_DOUBLE_EQ(0.83233741162176433, subpoint.GetLongitude());
    ASSERT_DOUBLE_EQ(-0.34238142277532951, subpoint.GetLatitude());
    ASSERT_DOUBLE_EQ(1.0586118481814565e-12, subpoint.GetAltitude());
}