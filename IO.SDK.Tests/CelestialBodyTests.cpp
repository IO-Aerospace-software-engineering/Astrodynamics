#include <gtest/gtest.h>
#include <CelestialBody.h>
#include <OrbitalParameters.h>
#include <TDB.h>
#include <limits>
#include <StateVector.h>
#include <memory>
#include <InertialFrames.h>
#include <Aberrations.h>
#include "Constraints/RelationalOperator.h"

using namespace std::chrono_literals;

TEST(CelestialBody, SphereOfInfluence)
{
	double res = IO::SDK::Body::SphereOfInfluence(150000000000, 1.32712440018E+20, 3.98600435436E+14);
	ASSERT_DOUBLE_EQ(927132302.95950806, res);

	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	// IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);

	ASSERT_DOUBLE_EQ(925064672.53459013, earth->GetSphereOfInfluence());
	ASSERT_DOUBLE_EQ(std::numeric_limits<double>::infinity(), sun->GetSphereOfInfluence());
}

TEST(CelestialBody, HillSphere)
{
	double res = IO::SDK::Body::HillSphere(150000000000, 0, 1.32712440018E+20, 3.98600435436E+14);
	ASSERT_DOUBLE_EQ(1500581377.2140491, res);

	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	// IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);

	ASSERT_DOUBLE_EQ(1471599696.8168514, earth->GetHillSphere());
	ASSERT_DOUBLE_EQ(std::numeric_limits<double>::infinity(), sun->GetHillSphere());
}

TEST(CelestialBody, GetStateVector)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);

	double expectedData[6]{-2.6795375379297768E+10, 1.3270111352322429E+11, 5.7525334752378304E+10, -29765.580095900841, -5075.3399173890839, -2200.9299676732885};
	auto sv = earth->ReadEphemeris( IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch,*sun);
	ASSERT_EQ(IO::SDK::OrbitalParameters::StateVector(sun, expectedData, epoch, IO::SDK::Frames::InertialFrames::GetICRF()), sv);

	//second overload
	auto sv2 = earth->ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch);
	ASSERT_EQ(IO::SDK::OrbitalParameters::StateVector(sun, expectedData, epoch, IO::SDK::Frames::InertialFrames::GetICRF()), sv2);
}

TEST(CelestialBody, GetRelativeStateVector)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	auto marsBarycenter = std::make_shared<IO::SDK::Body::CelestialBody>(4, sun);

	double expectedData[6]{1.1967701118722568E+11, 5.5305597076056137E+10, 2.6202720828289268E+10, 8.5989974247898281E+03, 1.5803131615538015E+04, 7.6926453157571395E+03};
	auto sv = earth->GetRelativeStatevector(marsBarycenter->ReadEphemeris( IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch,*sun));
	ASSERT_EQ(IO::SDK::OrbitalParameters::StateVector(earth, expectedData, epoch, IO::SDK::Frames::InertialFrames::GetICRF()), sv);
}

TEST(CelestialBody, IsInSphereOfInfluence)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	auto marsBarycenter = std::make_shared<IO::SDK::Body::CelestialBody>(4, sun);

	ASSERT_FALSE(earth->IsInSphereOfInfluence(marsBarycenter->ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch,*sun)));

	auto fictiveBody = IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(900000000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 1000.0, 0.0), epoch, IO::SDK::Frames::InertialFrames::GetICRF());
	ASSERT_TRUE(earth->IsInSphereOfInfluence(fictiveBody));
}

TEST(CelestialBody, IsInHillSphere)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	auto marsBarycenter = std::make_shared<IO::SDK::Body::CelestialBody>(4, sun);

	ASSERT_FALSE(earth->IsInHillSphere(marsBarycenter->ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, epoch,*sun)));

	auto fictiveBody = IO::SDK::OrbitalParameters::StateVector(earth, IO::SDK::Math::Vector3D(1400000000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 1000.0, 0.0), epoch, IO::SDK::Frames::InertialFrames::GetICRF());
	ASSERT_TRUE(earth->IsInHillSphere(fictiveBody));
}

TEST(CelestialBody, GetRadii)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	ASSERT_EQ(IO::SDK::Math::Vector3D(6378.1366, 6378.1366, 6356.7519), earth->GetRadius());
}

TEST(CelestialBody, GetFlattening)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	ASSERT_DOUBLE_EQ(0.0033528131084554717, earth->GetFlattening());
}

TEST(CelestialBody, GetAngularVelocity)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	ASSERT_DOUBLE_EQ(7.2921150187632176e-05, earth->GetAngularVelocity(epoch));
}

TEST(CelestialBody, GetSideralRotationPeriod)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	ASSERT_DOUBLE_EQ(23.934472337633899, earth->GetSideralRotationPeriod(epoch).GetHours().count());
}

TEST(CelestialBody, FindDistanceConstraint)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth);

	auto searchWindow = IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB("2007 JAN 1"), IO::SDK::Time::TDB("2007 APR 1"));
	auto results = earth->FindWindowsOnDistanceConstraint(searchWindow, *moon, *earth, IO::SDK::Constraints::RelationalOperator::GreaterThan(), IO::SDK::AberrationsEnum::None, 400000000.0, IO::SDK::Time::TimeSpan(86400s));

	ASSERT_EQ(4, results.size());
	ASSERT_STREQ("2007-01-08 00:11:07.628591 (TDB)", results[0].GetStartDate().ToString().c_str());
	ASSERT_STREQ("2007-01-13 06:37:47.948144 (TDB)", results[0].GetEndDate().ToString().c_str());
	ASSERT_STREQ("2007-03-29 22:53:58.151896 (TDB)", results[3].GetStartDate().ToString().c_str());
	ASSERT_STREQ("2007-04-01 00:01:05.185654 (TDB)", results[3].GetEndDate().ToString().c_str());
}

TEST(CelestialBody, FindOccultationConstraint)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun);
	auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth);

	auto searchWindow = IO::SDK::Time::Window<IO::SDK::Time::TDB>(IO::SDK::Time::TDB("2001 DEC 13"), IO::SDK::Time::TDB("2001 DEC 15"));
	auto results = earth->FindWindowsOnOccultationConstraint(searchWindow,*sun, *moon,IO::SDK::OccultationType::Any(), IO::SDK::AberrationsEnum::LT, IO::SDK::Time::TimeSpan(240s));

	ASSERT_EQ(1, results.size());
	ASSERT_STREQ("2001-12-14 20:10:15.410588 (TDB)", results[0].GetStartDate().ToString().c_str());
	ASSERT_STREQ("2001-12-14 21:35:49.100520 (TDB)", results[0].GetEndDate().ToString().c_str());
}