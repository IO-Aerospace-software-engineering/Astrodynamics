#include <gtest/gtest.h>
#include <string>
#include "TestsConstants.h"
#include <CelestialBody.h>
#include <EquinoctialElements.h>
#include <OrbitalParameters.h>
#include <SpiceUsr.h>
#include <cmath>
#include <Constants.h>
#include <TDB.h>
#include <InertialFrames.h>
#include <iostream>

using namespace std::chrono_literals;
TEST(EquinoctialElements, Initialization)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double gm = earth->GetMu();
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double n = sqrt(gm / a) / a;
	double argp = 30. * rpd_c();
	double node = 15. * rpd_c();
	double inc = 10. * rpd_c();
	double m0 = 45. * rpd_c();

	//equinoctial elements
	double h = ecc * sin(argp + node);
	double k = ecc * cos(argp + node);
	double p2 = tan(inc / 2.) * sin(node);
	double q = tan(inc / 2.) * cos(node);
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, IO::SDK::Time::TDB(120.0s), a, h, k, p2, q, L, 2.0, 3.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	ASSERT_DOUBLE_EQ(3.0, eq.GetAscendingNodeLongitudeRate());
	ASSERT_DOUBLE_EQ(IO::SDK::Constants::PI2, eq.GetDeclinationOfPole());
	ASSERT_DOUBLE_EQ(120.0, eq.GetEpoch().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(h, eq.GetH());
	ASSERT_DOUBLE_EQ(k, eq.GetK());
	ASSERT_DOUBLE_EQ(p2, eq.GetP());
	ASSERT_DOUBLE_EQ(q, eq.GetQ());
	ASSERT_DOUBLE_EQ(L, eq.GetL());
	ASSERT_DOUBLE_EQ(n, eq.GetMeanAnomalyRate());
	ASSERT_DOUBLE_EQ(2.0, eq.GetPeriapsisLongitudeRate());
	ASSERT_DOUBLE_EQ(-IO::SDK::Constants::PI2, eq.GetRightAscensionOfPole());
	ASSERT_DOUBLE_EQ(a, eq.GetSemiMajorAxis());
	ASSERT_EQ(earth.get(), eq.GetCenterOfMotion().get());
}

TEST(EquinoctialElements, InitializationFromKeplerian)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 6.780E+06;
	double gm = earth->GetMu();
	double ecc = 0.5;
	double a = p / (1. - ecc);
	double n = sqrt(gm / a) / a;
	double argp = 30. * rpd_c();
	double node = 15. * rpd_c();
	double inc = 10. * rpd_c();
	double m0 = 45. * rpd_c();

	//equinoctial elements
	double h = ecc * sin(argp + node);
	double k = ecc * cos(argp + node);
	double p2 = tan(inc / 2.) * sin(node);
	double q = tan(inc / 2.) * cos(node);
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, a, ecc, inc, argp, node, m0, 2.0, 3.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Time::TDB(120.0s), IO::SDK::Frames::InertialFrames::ICRF);

	ASSERT_DOUBLE_EQ(3.0, eq.GetAscendingNodeLongitudeRate());
	ASSERT_DOUBLE_EQ(IO::SDK::Constants::PI2, eq.GetDeclinationOfPole());
	ASSERT_DOUBLE_EQ(120.0, eq.GetEpoch().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(h, eq.GetH());
	ASSERT_DOUBLE_EQ(k, eq.GetK());
	ASSERT_DOUBLE_EQ(p2, eq.GetP());
	ASSERT_DOUBLE_EQ(q, eq.GetQ());
	ASSERT_DOUBLE_EQ(L, eq.GetL());
	ASSERT_DOUBLE_EQ(n, eq.GetMeanAnomalyRate());
	ASSERT_DOUBLE_EQ(2.0, eq.GetPeriapsisLongitudeRate());
	ASSERT_DOUBLE_EQ(-IO::SDK::Constants::PI2, eq.GetRightAscensionOfPole());
	ASSERT_DOUBLE_EQ(a, eq.GetSemiMajorAxis());
	ASSERT_EQ(earth.get(), eq.GetCenterOfMotion().get());
	ASSERT_DOUBLE_EQ(ecc, eq.GetEccentricity());
	ASSERT_DOUBLE_EQ(inc, eq.GetInclination());
	ASSERT_DOUBLE_EQ(argp, eq.GetPeriapsisArgument());
	ASSERT_DOUBLE_EQ(m0, eq.GetMeanAnomaly());
}

TEST(EquinoctialElements, GetPeriod)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 30. * rpd_c();
	double node = 15. * rpd_c();
	double inc = 10. * rpd_c();
	double m0 = 45. * rpd_c();

	//equinoctial elements
	double h = ecc * sin(argp + node);
	double k = ecc * cos(argp + node);
	double p2 = tan(inc / 2.) * sin(node);
	double q = tan(inc / 2.) * cos(node);
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, IO::SDK::Time::TDB(120.0s), a, h, k, p2, q, L, 2.0, 3.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	ASSERT_DOUBLE_EQ(11655.937761769412, eq.GetPeriod().GetSeconds().count());
}

TEST(EquinoctialElements, GetStateVector)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 30.0 * rpd_c();
	double node = 15.0 * rpd_c();
	double inc = 10.0 * rpd_c();
	double m0 = 45.0 * rpd_c();
	IO::SDK::Time::TDB t0{-100000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);
	double k = ecc * cos(argp + node);
	double p2 = tan(inc / 2.) * sin(node);
	double q = tan(inc / 2.) * cos(node);
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	IO::SDK::Time::TimeSpan et = t0.Substract(IO::SDK::Time::TDB(10000.0s));
	auto sv = eq.GetStateVector(IO::SDK::Time::TDB(et.GetSeconds() + 250s));

	ASSERT_DOUBLE_EQ(-10732167.450808318, sv.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(3902505.7550668186, sv.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(1154451.6100243214, sv.GetPosition().GetZ());
	ASSERT_DOUBLE_EQ(-2540.7668779537798, sv.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(-5152.2692064337361, sv.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(-761.57580784783909, sv.GetVelocity().GetZ());
}

TEST(EquinoctialElements, GetStateVectorFromKeplerian)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 0.0 * rpd_c();
	double node = 0.0 * rpd_c();
	double inc = 0.0 * rpd_c();
	double m0 = 0.0 * rpd_c();
	IO::SDK::Time::TDB t0{0.0s};

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, a, ecc, inc, argp, node, m0, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, t0, IO::SDK::Frames::InertialFrames::ICRF);

	auto sv = eq.GetStateVector(t0);

	ASSERT_DOUBLE_EQ(1.0e7, sv.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(6.1232339957367665e-10, sv.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(0.0, sv.GetPosition().GetZ());
	ASSERT_DOUBLE_EQ(-4.0545819533597326e-13, sv.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(6621.6348357464212, sv.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(4.0545819533597326e-13, sv.GetVelocity().GetZ());
	ASSERT_EQ(t0, sv.GetEpoch());
}

TEST(EquinoctialElements, GetStateVectorFrom0Eccentricity)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.0;
	double a = p / (1. - ecc);
	double argp = 0.0 * rpd_c();
	double node = 0.0 * rpd_c();
	double inc = 0.0 * rpd_c();
	double m0 = 0.0 * rpd_c();
	IO::SDK::Time::TDB t0{0.0s};

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, a, ecc, inc, argp, node, m0, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, t0, IO::SDK::Frames::InertialFrames::ICRF);

	for (size_t i = 0; i < 360; i++)
	{
		auto v = eq.GetTrueAnomaly(IO::SDK::Time::TDB(std::chrono::duration<double>(eq.GetPeriod().GetSeconds().count() / 360 * i)));
		std::cout << i << " : " << v * IO::SDK::Constants::RAD_DEG << std::endl;
		//std::cout << i << " : " << eq.GetStateVector(i * IO::SDK::Constants::DEG_RAD).GetTrueAnomaly() * IO::SDK::Constants::RAD_DEG << std::endl;
	}
}

TEST(EquinoctialElements, GetEccentricity)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 30.0 * rpd_c();
	double node = 15.0 * rpd_c();
	double inc = 10.0 * rpd_c();
	double m0 = 45.0 * rpd_c();
	IO::SDK::Time::TDB t0{-100000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto e = eq.GetEccentricity();

	ASSERT_DOUBLE_EQ(0.1, e);
}

TEST(EquinoctialElements, GetInclination)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 30.0 * rpd_c();
	double node = 15.0 * rpd_c();
	double inc = 10.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 45.0 * rpd_c();
	IO::SDK::Time::TDB t0{-100000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto i = eq.GetInclination();

	ASSERT_DOUBLE_EQ(10.0 * IO::SDK::Constants::DEG_RAD, i);
}

TEST(EquinoctialElements, GetPeriapsisArgument)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 30.0 * rpd_c();
	double node = 15.0 * rpd_c();
	double inc = 10.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 45.0 * rpd_c();
	IO::SDK::Time::TDB t0{-100000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto w = eq.GetPeriapsisArgument();

	ASSERT_DOUBLE_EQ(30.0 * IO::SDK::Constants::DEG_RAD, w);
}

TEST(EquinoctialElements, GetRightAscendingNode)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 30.0 * rpd_c();
	double node = 15.0 * rpd_c();
	double inc = 10.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 45.0 * rpd_c();
	IO::SDK::Time::TDB t0{-100000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto o = eq.GetRightAscendingNodeLongitude();

	ASSERT_DOUBLE_EQ(15.0 * IO::SDK::Constants::DEG_RAD, o);
}

TEST(EquinoctialElements, GetMeanAnomalyAtEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double p = 1.0e7;
	double ecc = 0.1;
	double a = p / (1. - ecc);
	double argp = 30.0 * rpd_c();
	double node = 15.0 * rpd_c();
	double inc = 10.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 45.0 * rpd_c();
	IO::SDK::Time::TDB t0{-100000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto m0Res = eq.GetMeanAnomaly();

	ASSERT_DOUBLE_EQ(45.0 * IO::SDK::Constants::DEG_RAD, m0Res);
}

TEST(EquinoctialElements, GetSpecificOrbitalEnergy)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//ISS keplerian elements
	//double p = 1.0e7;
	double ecc = 8.258860265483704E-04;
	double a = 6.797371275322830E+06;
	double argp = 9.311325640521339E+01 * rpd_c();
	double node = 9.542543898089574E+00 * rpd_c();
	double inc = 5.167235936552875E+01 * IO::SDK::Constants::DEG_RAD;
	double m0 = 2.989409852022806E+02 * rpd_c();
	IO::SDK::Time::TDB t0{626417577.764200s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto j = eq.GetSpecificOrbitalEnergy();

	ASSERT_DOUBLE_EQ(-29320190.062530093, j);
}

TEST(EquinoctialElements, GetSpecificAngularMomentum)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//ISS keplerian elements
	double ecc = 8.258860265483704E-04;
	double a = 6.797371275322830E+06;
	double argp = 9.311325640521339E+01 * rpd_c();
	double node = 9.542543898089574E+00 * rpd_c();
	double inc = 5.167235936552875E+01 * IO::SDK::Constants::DEG_RAD;
	double m0 = 2.989409852022806E+02 * rpd_c();
	IO::SDK::Time::TDB t0{626417577.764200s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto mom = eq.GetSpecificAngularMomentum();

	ASSERT_DOUBLE_EQ(52052217071.821465, mom.Magnitude());
}

TEST(EquinoctialElements, GetTrueAnomalyAtEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//ISS keplerian elements
	double ecc = 8.258860265483704E-04;
	double a = 6.797371275322830E+06;
	double argp = 9.311325640521339E+01 * rpd_c();
	double node = 9.542543898089574E+00 * rpd_c();
	double inc = 5.167235936552875E+01 * IO::SDK::Constants::DEG_RAD;
	double m0 = 2.989409852022806E+02 * rpd_c();
	IO::SDK::Time::TDB t0{626417577.764200s};

	//equinoctial elements
	double h = ecc * sin(argp + node);	   //g
	double k = ecc * cos(argp + node);	   //f
	double p2 = tan(inc / 2.) * sin(node); //k
	double q = tan(inc / 2.) * cos(node);  //h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto v = eq.GetTrueAnomaly();

	ASSERT_DOUBLE_EQ(5.2160582426429993, v);
}

TEST(EquinoctialElements, GetISSMeanAnomaly)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double ecc = 8.258860265483704E-04;
	double a = 6.797371275322830E+06;
	double argp = 9.311325640521339E+01 * rpd_c();
	double node = 9.542543898089574E+00 * rpd_c();
	double inc = 5.167235936552875E+01 * IO::SDK::Constants::DEG_RAD;
	double m0 = 2.989409852022806E+02 * rpd_c();
	IO::SDK::Time::TDB t0{626417577.764200s};

	//equinoctial elements
	double h = ecc * sin(argp + node);		//g
	double k = ecc * cos(argp + node);		//f
	double p2 = tan(inc / 2.0) * sin(node); //k
	double q = tan(inc / 2.0) * cos(node);	//h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto m0Res = eq.GetMeanAnomaly();

	ASSERT_DOUBLE_EQ(m0, m0Res);
}

TEST(EquinoctialElements, GetSemiMajorAxis)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double ecc = 8.258860265483704E-04;
	double a = 6.797371275322830E+06;
	double argp = 9.311325640521339E+01 * rpd_c();
	double node = 9.542543898089574E+00 * rpd_c();
	double inc = 5.167235936552875E+01 * IO::SDK::Constants::DEG_RAD;
	double m0 = 2.989409852022806E+02 * rpd_c();
	IO::SDK::Time::TDB t0{626417577.764200s};

	//equinoctial elements
	double h = ecc * sin(argp + node);		//g
	double k = ecc * cos(argp + node);		//f
	double p2 = tan(inc / 2.0) * sin(node); //k
	double q = tan(inc / 2.0) * cos(node);	//h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto semiMajorAxis = eq.GetSemiMajorAxis();

	ASSERT_DOUBLE_EQ(a, semiMajorAxis);
}

TEST(EquinoctialElements, GetTimeToMeanAnomaly)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double ecc = 0.5;
	double a = 7136635.456;
	double argp = 20.0 * rpd_c();
	double node = 45.0 * rpd_c();
	double inc = 60.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 10.0 * rpd_c();
	IO::SDK::Time::TDB t0{60000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);		//g
	double k = ecc * cos(argp + node);		//f
	double p2 = tan(inc / 2.0) * sin(node); //k
	double q = tan(inc / 2.0) * cos(node);	//h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto epoch = eq.GetTimeToMeanAnomaly(IO::SDK::Constants::PI2);

	ASSERT_DOUBLE_EQ(60001333.333344065, epoch.GetSecondsFromJ2000().count());
}

TEST(EquinoctialElements, GetMeanAnomalyForEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double ecc = 0.5;
	double a = 7136635.456;
	double argp = 20.0 * rpd_c();
	double node = 45.0 * rpd_c();
	double inc = 60.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 10.0 * rpd_c();
	IO::SDK::Time::TDB t0{60000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);		//g
	double k = ecc * cos(argp + node);		//f
	double p2 = tan(inc / 2.0) * sin(node); //k
	double q = tan(inc / 2.0) * cos(node);	//h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto meanAnomaly = eq.GetMeanAnomaly(IO::SDK::Time::TDB(60001333.333344065s)); //=90� mean anomaly;

	ASSERT_NEAR(IO::SDK::Constants::PI2, meanAnomaly, IO::SDK::Test::Constants::ANGULAR_ACCURACY);
}

TEST(EquinoctialElements, GetTimeToTrueAnomaly)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double ecc = 0.5;
	double a = 7136635.456;
	double argp = 20.0 * rpd_c();
	double node = 45.0 * rpd_c();
	double inc = 60.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 10.0 * rpd_c();
	IO::SDK::Time::TDB t0{60000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);		//g
	double k = ecc * cos(argp + node);		//f
	double p2 = tan(inc / 2.0) * sin(node); //k
	double q = tan(inc / 2.0) * cos(node);	//h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto epoch = eq.GetTimeToTrueAnomaly(2.446560878); //=90� mean anomaly;

	ASSERT_DOUBLE_EQ(60001333.333344109, epoch.GetSecondsFromJ2000().count());
}

TEST(EquinoctialElements, GetTrueAnomalyForEpoch)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double ecc = 0.5;
	double a = 7136635.456;
	double argp = 20.0 * rpd_c();
	double node = 45.0 * rpd_c();
	double inc = 60.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 10.0 * rpd_c();
	IO::SDK::Time::TDB t0{60000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);		//g
	double k = ecc * cos(argp + node);		//f
	double p2 = tan(inc / 2.0) * sin(node); //k
	double q = tan(inc / 2.0) * cos(node);	//h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	auto trueAnomaly = eq.GetTrueAnomaly(IO::SDK::Time::TDB(60001333.333344109s)); //=90� mean anomaly;

	ASSERT_DOUBLE_EQ(2.4465608784128867, trueAnomaly);
}

TEST(EquinoctialElements, TrajectoryType)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");

	//keplerian elements
	double ecc = 0.5;
	double a = 7136635.456;
	double argp = 20.0 * rpd_c();
	double node = 45.0 * rpd_c();
	double inc = 60.0 * IO::SDK::Constants::DEG_RAD;
	double m0 = 10.0 * rpd_c();
	IO::SDK::Time::TDB t0{60000000.0s};

	//equinoctial elements
	double h = ecc * sin(argp + node);		//g
	double k = ecc * cos(argp + node);		//f
	double p2 = tan(inc / 2.0) * sin(node); //k
	double q = tan(inc / 2.0) * cos(node);	//h
	double L = m0 + argp + node;

	IO::SDK::OrbitalParameters::EquinoctialElements eq(earth, t0, a, h, k, p2, q, L, 0.0, 0.0, -IO::SDK::Constants::PI2, IO::SDK::Constants::PI2, IO::SDK::Frames::InertialFrames::ICRF);

	ASSERT_TRUE(eq.IsElliptical());
	ASSERT_FALSE(eq.IsParabolic());
	ASSERT_FALSE(eq.IsHyperbolic());
}