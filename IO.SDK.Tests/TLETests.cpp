#include <gtest/gtest.h>
#include <string>
#include <TLE.h>
#include "TestsConstants.h"
#include <TDB.h>
#include <chrono>
#include <CelestialBody.h>
#include <memory>

using namespace std::chrono_literals;
TEST(TLE, Initialization)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);
}

TEST(TLE, GetSatelliteName)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	ASSERT_STREQ("ISS", tle.GetSatelliteName().data());
}

TEST(TLE, GetBalisticCoefficient)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	ASSERT_DOUBLE_EQ(5.0653939419425700e-10, tle.GetBalisticCoefficient());
}

TEST(TLE, GetSecondDerivativeOfMeanMotion)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	ASSERT_DOUBLE_EQ(0.0, tle.GetSecondDerivativeOfMeanMotion());
}

TEST(TLE, GetDragTerm)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	ASSERT_DOUBLE_EQ(0.1027e-3, tle.GetDragTerm());
}

TEST(TLE, GetPeriod)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	auto period = tle.GetPeriod().GetSeconds().count();

	ASSERT_DOUBLE_EQ(5576.6781455895143, period);
}

TEST(TLE, GetCenterOfMotion)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	const auto res = tle.GetCenterOfMotion();

	ASSERT_EQ(earth.get(), res.get());
}

TEST(TLE, GetEccentricity)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetEccentricity();

	ASSERT_DOUBLE_EQ(0.0000493, res);
}

TEST(TLE, GetEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	IO::SDK::Time::TDB res = tle.GetEpoch();

	ASSERT_DOUBLE_EQ(664419082.84759140, res.GetSecondsFromJ2000().count());
}

TEST(TLE, GetInclination)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetInclination();

	ASSERT_DOUBLE_EQ(0.9013281683026676, res);
}

TEST(TLE, GetMeanAnomaly)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetMeanAnomaly();

	ASSERT_DOUBLE_EQ(0.68479738531249512, res);
}

TEST(TLE, GetMeanMotion)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetMeanMotion();

	ASSERT_DOUBLE_EQ(0.0011266896068134818, res);
}

TEST(TLE, GetPeriapsisArgument)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetPeriapsisArgument();

	ASSERT_DOUBLE_EQ(5.6003339639830649, res);
}

TEST(TLE, GetRightAscendingNodeLongitude)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetRightAscendingNodeLongitude();

	ASSERT_DOUBLE_EQ(6.1615568022666061, res);
}

TEST(TLE, GetSemiMajorAxis)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetSemiMajorAxis();

	ASSERT_DOUBLE_EQ(6803376.2171725733, res);
}

TEST(TLE, GetTimeToMeanAnomaly)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	IO::SDK::Time::TDB res = tle.GetTimeToMeanAnomaly(IO::SDK::Constants::PI2);

	ASSERT_DOUBLE_EQ(664419869.22117305, res.GetSecondsFromJ2000().count());
}

TEST(TLE, GetTimeToTrueAnomaly)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	IO::SDK::Time::TDB res = tle.GetTimeToTrueAnomaly(IO::SDK::Constants::PI2);

	ASSERT_DOUBLE_EQ(664419869.13365996, res.GetSecondsFromJ2000().count());
}

TEST(TLE, GetTrueAnomaly)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetTrueAnomaly();

	ASSERT_DOUBLE_EQ(0.68485975437583080, res);
}

TEST(TLE, GetTrueAnomalyAtEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetTrueAnomaly(IO::SDK::Time::TDB(664419869.13365996s));

	ASSERT_NEAR(IO::SDK::Constants::PI2, res, IO::SDK::Test::Constants::ANGULAR_ACCURACY);
}

TEST(TLE, GetMeanAnomalyAtEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	double res = tle.GetMeanAnomaly(IO::SDK::Time::TDB(664419869.22117305s));

	ASSERT_NEAR(IO::SDK::Constants::PI2, res, IO::SDK::Test::Constants::ANGULAR_ACCURACY);
}

TEST(TLE, TrajectoryType)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);

	ASSERT_TRUE(tle.IsElliptical());
	ASSERT_FALSE(tle.IsParabolic());
	ASSERT_FALSE(tle.IsHyperbolic());
}

TEST(TLE, GetStateVectorAtEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::string lines[3]{"ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054", "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"};
	IO::SDK::OrbitalParameters::TLE tle(earth, lines);
	IO::SDK::Time::TDB epoch("2021-01-20T18:50:13.663106"); //utc

	auto stateVector = tle.GetStateVector(epoch); //2021-Jan-20 18:51:22.8476
	ASSERT_DOUBLE_EQ(4363669.4588805977, stateVector.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(-3627809.8122968217, stateVector.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(-3747415.3046325566, stateVector.GetPosition().GetZ());
	ASSERT_DOUBLE_EQ(5805.8240970581246, stateVector.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(2575.7227314383458, stateVector.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(4271.5975567314108, stateVector.GetVelocity().GetZ());
	ASSERT_EQ(epoch, stateVector.GetEpoch());
}