#include <gtest/gtest.h>
#include <ConicOrbitalElements.h>
#include <CelestialBody.h>
#include <cmath>
#include <TestsConstants.h>
#include <Constants.h>
#include <TimeSpan.h>
#include <chrono>
#include <TDB.h>
#include <memory>
#include <InertialFrames.h>

using namespace std::chrono_literals;
TEST(ConicOrbitalElements, Initialization)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 7000000.0, 0.5, 2.0, 3.0, 1.0, 1.57, IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_EQ(earth.get(), conic.GetCenterOfMotion().get());
	ASSERT_DOUBLE_EQ(7000000.0, conic.GetPerifocalDistance());
	ASSERT_DOUBLE_EQ(0.5, conic.GetEccentricity());
	ASSERT_DOUBLE_EQ(2.0, conic.GetInclination());
	ASSERT_DOUBLE_EQ(3.0, conic.GetRightAscendingNodeLongitude());
	ASSERT_DOUBLE_EQ(1.0, conic.GetPeriapsisArgument());
	ASSERT_DOUBLE_EQ(1.57, conic.GetMeanAnomaly());
	ASSERT_DOUBLE_EQ(2.0203258275202955, conic.GetEccentricAnomaly(IO::Astrodynamics::Time::TDB(100.0s)));
	ASSERT_DOUBLE_EQ(2.4460955683630288, conic.GetTrueAnomaly());
	ASSERT_DOUBLE_EQ(14000000.0, conic.GetSemiMajorAxis());
	ASSERT_DOUBLE_EQ(100.0, conic.GetEpoch().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(16485.534686666488, conic.GetPeriod().GetSeconds().count());

	double elts[11]{6794349.7510811854, 1.353139738203394E-03, 5.171921958517460E+01, 3.257605322534260E+01, 1.062574316262159E+02, 4.541224977546975E+01, 663724800.00001490, 3.986004418e14, 4.552280986634524E+01, 6.800803544958167E+06, 5581.5051305524184};
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conicFromArray(earth, elts, IO::Astrodynamics::Frames::InertialFrames::GetICRF());
	ASSERT_EQ(earth.get(), conicFromArray.GetCenterOfMotion().get());
	ASSERT_EQ(6794349.7510811854, conicFromArray.GetPerifocalDistance());
	ASSERT_EQ(1.353139738203394E-03, conicFromArray.GetEccentricity());
	ASSERT_EQ(5.171921958517460E+01, conicFromArray.GetInclination());
	ASSERT_EQ(3.257605322534260E+01, conicFromArray.GetRightAscendingNodeLongitude());
	ASSERT_EQ(1.062574316262159E+02, conicFromArray.GetPeriapsisArgument());
	ASSERT_EQ(4.541224977546975E+01, conicFromArray.GetMeanAnomaly());
	ASSERT_EQ(4.552280986634524E+01, conicFromArray.GetTrueAnomaly());
	ASSERT_EQ(6.800803544958167E+06, conicFromArray.GetSemiMajorAxis());
	ASSERT_EQ(663724800.00001490, conicFromArray.GetEpoch().GetSecondsFromJ2000().count());
	ASSERT_EQ(5581.5051305524184, conicFromArray.GetPeriod().GetSeconds().count());
}

TEST(ConicOrbitalElements, GetMeanAnomaly)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 7136635.417, 0.0, 0.0, 0.0, 0.0, 0.0, IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	//180deg
	ASSERT_NEAR(IO::Astrodynamics::Constants::PI, conic.GetMeanAnomaly(IO::Astrodynamics::Time::TDB(3100.0s)), IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY);

	//180deg in past
	ASSERT_NEAR(IO::Astrodynamics::Constants::PI, conic.GetMeanAnomaly(IO::Astrodynamics::Time::TDB(-2900.0s)), IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY);

	//90deg
	ASSERT_NEAR(IO::Astrodynamics::Constants::PI2, conic.GetMeanAnomaly(IO::Astrodynamics::Time::TDB(1600.0s)), IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY);

	//90deg in past (270deg)

	ASSERT_NEAR(-IO::Astrodynamics::Constants::PI2 + IO::Astrodynamics::Constants::_2PI, conic.GetMeanAnomaly(IO::Astrodynamics::Time::TDB(-1400.0s)), IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY);

	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements orbitalParams1(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_DOUBLE_EQ(7.2921598035841106e-05, orbitalParams1.GetMeanMotion());
}

TEST(ConicOrbitalElements, GetEccentricAnomaly)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 7000000.0, 0.5, 0.0, 0.0, 0.0, 0.0, IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	double eccentricAnomaly = conic.GetEccentricAnomaly(IO::Astrodynamics::Time::TDB(374.7589113s));
	ASSERT_NEAR(0.2079441345897452, eccentricAnomaly, IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY); //11.914321389deg

	eccentricAnomaly = conic.GetEccentricAnomaly(IO::Astrodynamics::Time::TDB(8342.767343s));
	ASSERT_NEAR(IO::Astrodynamics::Constants::PI, eccentricAnomaly, IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY); //180deg

	eccentricAnomaly = conic.GetEccentricAnomaly(IO::Astrodynamics::Time::TDB(-8142.767343s));
	ASSERT_NEAR(IO::Astrodynamics::Constants::PI, eccentricAnomaly, IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY); //180deg
}

TEST(ConicOrbitalElements, GetTrueAnomaly)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 7000000.0, 0.5, 2.0, 3.0, 4.0, 0.0, IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	double trueAnomaly = conic.GetTrueAnomaly(IO::Astrodynamics::Time::TDB(374.7589113s));
	ASSERT_NEAR(0.35761273441580932, trueAnomaly, IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY); //20.489700557�

	trueAnomaly = conic.GetTrueAnomaly(IO::Astrodynamics::Time::TDB(8342.767343s));
	ASSERT_NEAR(IO::Astrodynamics::Constants::PI, trueAnomaly, IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY); //180�

	trueAnomaly = conic.GetTrueAnomaly(IO::Astrodynamics::Time::TDB(-8142.767343s));
	ASSERT_NEAR(IO::Astrodynamics::Constants::PI, trueAnomaly, IO::Astrodynamics::Test::Constants::ANGULAR_ACCURACY); //180�
}

TEST(ConicOrbitalElements, GetTimeToMeanAnomaly)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 7000000.0, 0.5, 2.0, 3.0, 4.0, 0.0, IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	IO::Astrodynamics::Time::TDB epoch = conic.GetTimeToMeanAnomaly(IO::Astrodynamics::Constants::PI2); // to 90 �
	ASSERT_NEAR(4221.3836716666292, epoch.GetSecondsFromJ2000().count(), IO::Astrodynamics::Test::Constants::TIME_ACCURACY);

	epoch = conic.GetTimeToMeanAnomaly(-(IO::Astrodynamics::Constants::PI2 + IO::Astrodynamics::Constants::PI)); //From reverse cadran 300� to -270�(90�)
	ASSERT_NEAR(4221.3836716666292, epoch.GetSecondsFromJ2000().count(), IO::Astrodynamics::Test::Constants::TIME_ACCURACY);
}

TEST(ConicOrbitalElements, GetTimeToTrueAnomaly)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 7000000.0, 0.5, 2.0, 3.0, 4.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	IO::Astrodynamics::Time::TDB epoch = conic.GetTimeToTrueAnomaly(IO::Astrodynamics::Constants::PI); // to 180 d
	ASSERT_NEAR(8242.7673433332584, epoch.GetSecondsFromJ2000().count(), IO::Astrodynamics::Test::Constants::TIME_ACCURACY);

	epoch = conic.GetTimeToTrueAnomaly(2.446560878); //  to 140.17761d true anomaly = 90d mean anomaly �
	ASSERT_NEAR(4121.3836716666292, epoch.GetSecondsFromJ2000().count(), IO::Astrodynamics::Test::Constants::TIME_ACCURACY);

	epoch = conic.GetTimeToTrueAnomaly(-IO::Astrodynamics::Constants::PI); // to 180d true anomaly = 180d mean anomaly �
	ASSERT_NEAR(8242.7673433332584, epoch.GetSecondsFromJ2000().count(), IO::Astrodynamics::Test::Constants::TIME_ACCURACY);

	epoch = conic.GetTimeToTrueAnomaly(IO::Astrodynamics::Constants::PI + IO::Astrodynamics::Constants::PI2); // to 270d true anomaly = 325d mean anomaly �
	ASSERT_NEAR(14874.064525876782, epoch.GetSecondsFromJ2000().count(), IO::Astrodynamics::Test::Constants::TIME_ACCURACY);
}

TEST(ConicOrbitalElements, ToStateVector)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	double a = 6.800803544958167E+06;
	IO::Astrodynamics::Time::TimeSpan ts(std::chrono::duration<double>(IO::Astrodynamics::Constants::_2PI * std::sqrt((a * a * a) / earth->GetMu())));

	double perifocDist = std::sqrt(std::pow(-6.116559469556896E+06, 2) + std::pow(-1.546174698676721E+06, 2) + std::pow(2.521950157430313E+06, 2));

	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(
		earth,
		perifocDist, //6794349.7510811854,
		1.353139738203394E-03,
		5.171921958517460E+01 * IO::Astrodynamics::Constants::DEG_RAD,
		3.257605322534260E+01 * IO::Astrodynamics::Constants::DEG_RAD,
		1.062574316262159E+02 * IO::Astrodynamics::Constants::DEG_RAD,
		4.541224977546975E+01 * IO::Astrodynamics::Constants::DEG_RAD,
		IO::Astrodynamics::Time::TDB(663724800.00001490s), IO::Astrodynamics::Frames::InertialFrames::GetICRF()); //"2021-01-12T11:58:50.816" UTC

	IO::Astrodynamics::OrbitalParameters::StateVector sv = conic.ToStateVector(IO::Astrodynamics::Time::TDB(663724800.00001490s));

	//Low accuracy due to conical propagation
	ASSERT_NEAR(-6.116559469556896E+06, sv.GetPosition().GetX(), 3e3);
	ASSERT_NEAR(-1.546174698676721E+06, sv.GetPosition().GetY(), 3e3);
	ASSERT_NEAR(2.521950157430313E+06, sv.GetPosition().GetZ(), 3e3);

	ASSERT_NEAR(-8.078523150700097E+02, sv.GetVelocity().GetX(), 0.2);
	ASSERT_NEAR(-5.477647950892673E+03, sv.GetVelocity().GetY(), 1.2);
	ASSERT_NEAR(-5.297615757935174E+03, sv.GetVelocity().GetZ(), 1.1);
}

TEST(ConicOrbitalElements, GetStateVectorFromToConic)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL

    auto parkingOrbit = std::make_shared<IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements>(earth,
                                                                                           6700000.0,
                                                                                           0.3,
                                                                                           50.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                           41.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                           0.0 * IO::Astrodynamics::Constants::DEG_RAD,
                                                                                           0.0,
                                                                                           IO::Astrodynamics::Time::TDB("2021-03-02T00:00:00"),
                                                                                           IO::Astrodynamics::Frames::InertialFrames::GetICRF());

    IO::Astrodynamics::OrbitalParameters::StateVector sv = parkingOrbit->ToStateVector();

    //Low accuracy due to conical propagation
    ASSERT_DOUBLE_EQ(6700000.0, sv.GetPerigeeVector().Magnitude());
    ASSERT_NEAR(0.3, sv.GetEccentricity(),1E-09);
    ASSERT_NEAR(50.0 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetInclination(),1E-09);
    ASSERT_NEAR(41.0 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetRightAscendingNodeLongitude(),1E-09);
    ASSERT_NEAR(0.0, sv.GetPeriapsisArgument(),1E-09);
    ASSERT_NEAR(IO::Astrodynamics::Constants::_2PI, sv.GetMeanAnomaly(),1E-09);
    ASSERT_NEAR(IO::Astrodynamics::Constants::_2PI, sv.GetTrueAnomaly(),1E-09);
    ASSERT_NEAR(9571428.5714285765, sv.GetSemiMajorAxis(),1E-09);


}

TEST(ConicOrbitalElements, IsElliptical)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::Time::TimeSpan ts(6000.0s);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 1.0, 0.5, 2.0, 3.0, 4.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_TRUE(conic.IsElliptical());
}

TEST(ConicOrbitalElements, IsHyperbolic)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::Time::TimeSpan ts(0.0s);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 1.0, 1.5, 2.0, 3.0, 4.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_TRUE(conic.IsHyperbolic());
}

TEST(ConicOrbitalElements, IsParabolic)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::Time::TimeSpan ts(0.0s);
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conic(earth, 1.0, 1.0, 2.0, 3.0, 4.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_TRUE(conic.IsParabolic());
}

TEST(ConicOrbitalElements, GetMeanMotion)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	double elts[11]{6794349.7510811854, 1.353139738203394E-03, 5.171921958517460E+01, 3.257605322534260E+01, 1.062574316262159E+02, 4.541224977546975E+01, 663724800.00001490, 3.986004418e14, 4.552280986634524E+01, 6.800803544958167E+06, 5581.5051305524184};
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conicFromArray(earth, elts, IO::Astrodynamics::Frames::InertialFrames::GetICRF());
	ASSERT_DOUBLE_EQ(0.0011257152255914383, conicFromArray.GetMeanMotion());

	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements orbitalParams1(earth, 42164000.0, 0.0, 0.0, 0.0, 0.0, 0.0, IO::Astrodynamics::Time::TDB(0.0s), IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_DOUBLE_EQ(7.2921598035841106e-05, orbitalParams1.GetMeanMotion());
}

TEST(ConicOrbitalElements, GetSpecificOrbitalEnergy)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	double elts[11]{6794349.7510811854, 1.353139738203394E-03, 5.171921958517460E+01, 3.257605322534260E+01, 1.062574316262159E+02, 4.541224977546975E+01, 663724800.00001490, 3.986004418e14, 4.552280986634524E+01, 6.800803544958167E+06, 5581.5051305524184};
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conicFromArray(earth, elts, IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_DOUBLE_EQ(-29293537.125013251, conicFromArray.GetSpecificOrbitalEnergy()); //iss energy
}

TEST(ConicOrbitalElements, GetSpecificAngularMomentum)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	double elts[11]{6794349.7510811854, 1.353139738203394E-03, 5.171921958517460E+01, 3.257605322534260E+01, 1.062574316262159E+02, 4.541224977546975E+01, 663724800.00001490, 3.986004418e14, 4.552280986634524E+01, 6.800803544958167E+06, 5581.5051305524184};
	IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conicFromArray(earth, elts, IO::Astrodynamics::Frames::InertialFrames::GetICRF());

	ASSERT_DOUBLE_EQ(52075861816.778732, conicFromArray.GetSpecificAngularMomentum().Magnitude());
}

TEST(ConicOrbitalElements, GetRADec)
{
	auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
	// IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);

	auto radec = earth->GetOrbitalParametersAtEpoch()->ToEquatorialCoordinates();

	ASSERT_DOUBLE_EQ(1.7678119732568962,radec.GetRA());
	ASSERT_DOUBLE_EQ(0.40200709658915335,radec.GetDec());
	ASSERT_DOUBLE_EQ(1.4710372695917715E+11,radec.GetRange());
}