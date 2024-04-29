/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <StateVector.h>
#include <InertialFrames.h>
#include <Constants.h>

using namespace std::chrono_literals;
TEST(StateVector, Initialization)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(1);
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());
	ASSERT_DOUBLE_EQ(1.0, sv.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(2.0, sv.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(3.0, sv.GetPosition().GetZ());

	ASSERT_DOUBLE_EQ(4.0, sv.GetVelocity().GetX());
	ASSERT_EQ(5.0, sv.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(6.0, sv.GetVelocity().GetZ());

	ASSERT_DOUBLE_EQ(100.0, sv.GetEpoch().GetSecondsFromJ2000().count());
	ASSERT_EQ(earth.get(), sv.GetCenterOfMotion().get());

	double state[6] = {11.0, 12.0, 13.0, 14.0, 15.0, 16.0};
	IO::Astrodynamics::OrbitalParameters::StateVector svFromState(earth, state, IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());

	ASSERT_DOUBLE_EQ(11.0, svFromState.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(12.0, svFromState.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(13.0, svFromState.GetPosition().GetZ());

	ASSERT_DOUBLE_EQ(14.0, svFromState.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(15.0, svFromState.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(16.0, svFromState.GetVelocity().GetZ());

	ASSERT_DOUBLE_EQ(100.0, svFromState.GetEpoch().GetSecondsFromJ2000().count());
	ASSERT_EQ(earth.get(), svFromState.GetCenterOfMotion().get());
}

TEST(StateVector, GetSpecificAngularMomentum)
{
	auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
	IO::Astrodynamics::OrbitalParameters::StateVector sv(sun, IO::Astrodynamics::Math::Vector3D(149.6e9, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 2.98e4, 0.0), IO::Astrodynamics::Time::TDB(0.0s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());
	ASSERT_DOUBLE_EQ(4458080000000000.0, sv.GetSpecificAngularMomentum().Magnitude()); //Earth momentum
}

TEST(StateVector, GetSpecificOrbitalEnergy)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());
	ASSERT_DOUBLE_EQ(-29305465.588067468, sv.GetSpecificOrbitalEnergy()); //Earth momentum
}

TEST(StateVector, GetSemiMajorAxis)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(6.800799983064672E+06, sv.GetSemiMajorAxis(), 1e2);
}

TEST(StateVector, GetEccentricity)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(1.352296372623237E-03, sv.GetEccentricity(), 1e-5);
}

TEST(StateVector, GetInclination)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(5.171933849805028E+01 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetInclination(), 1e-4);
}

TEST(StateVector, GetPeriapsisArgument)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(1.062027499375132E+02 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetPeriapsisArgument(), 1e-2);
}

TEST(StateVector, GetRAN)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(3.257645785453723E+01 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetRightAscendingNodeLongitude(), 1e-2);
}

TEST(StateVector, GetMeanAnomaly)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(4.546651762339189E+01 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetMeanAnomaly(), 1e-2);
}

TEST(StateVector, GetTrueAnomaly)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(4.557711170488279E+01 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetTrueAnomaly(), 1e-2);
}

TEST(StateVector, GetPeriod)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(5.581500745616037E+03, sv.GetPeriod().GetSeconds().count(), 1);
}

TEST(StateVector, GetMeanMotion)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(6.448974542312881E-02 * IO::Astrodynamics::Constants::DEG_RAD, sv.GetMeanMotion(), 1e-6);
}

TEST(StateVector, IsElliptical)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::Astrodynamics::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_TRUE(sv.IsElliptical());
}

TEST(StateVector, IsHyperbolic)
{
	//ISS
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 18000.0, 0.0), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_TRUE(sv.IsHyperbolic());
}

TEST(StateVector, IsParabolic)
{
	//FICTIVE
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	double escapeVelocity = std::sqrt((earth->GetMu() * 2.0) / 6800000.0);

	IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, escapeVelocity, 0.0), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_TRUE(sv.IsParabolic());
}

TEST(StateVector, Assignement)
{
	//FICTIVE
	auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);			//GEOPHYSICAL PROPERTIES provided by JPL
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun); //GEOPHYSICAL PROPERTIES provided by JPL

	//Define SV with earth as center of motion but the influence body is the moon in this case at 2021-01-01 00:00:00.000000 TDB
	IO::Astrodynamics::OrbitalParameters::StateVector sv{earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                         IO::Astrodynamics::Frames::InertialFrames::ICRF()};
	IO::Astrodynamics::OrbitalParameters::StateVector sv2{earth, IO::Astrodynamics::Math::Vector3D(10.0, 20.0, 30.0), IO::Astrodynamics::Math::Vector3D(40.0, 50.0, 60.0), IO::Astrodynamics::Time::TDB(1000.0s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF()};
	sv2 = sv;
	ASSERT_DOUBLE_EQ(sv.GetPosition().GetX(), sv2.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(sv.GetPosition().GetY(), sv2.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(sv.GetPosition().GetZ(), sv2.GetPosition().GetZ());
	ASSERT_DOUBLE_EQ(sv.GetVelocity().GetX(), sv2.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(sv.GetVelocity().GetY(), sv2.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(sv.GetVelocity().GetZ(), sv2.GetVelocity().GetZ());
	ASSERT_EQ(sv.GetEpoch(), sv2.GetEpoch());
}

TEST(StateVector, Frame)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 18000.0, 0.0), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());

	//Low accuracy due to conical propagation
	ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::ICRF(), sv.GetFrame());
	ASSERT_NE(IO::Astrodynamics::Frames::InertialFrames::Galactic(), sv.GetFrame());
}

TEST(StateVector, EccentricityVector)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 9000.0, 0.0), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());

	auto e = sv.GetEccentricityVector();

	ASSERT_DOUBLE_EQ(sv.GetEccentricity(), e.Magnitude());
	ASSERT_DOUBLE_EQ(1.0, e.Normalize().GetX());
	ASSERT_DOUBLE_EQ(0.0, e.Normalize().GetY());
	ASSERT_DOUBLE_EQ(0.0, e.Normalize().GetZ());
}

TEST(StateVector, PerigeeVector)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 9000.0, 0.0), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());

	auto p = sv.GetPerigeeVector();

	ASSERT_DOUBLE_EQ(6800000.0, p.Magnitude());
	ASSERT_DOUBLE_EQ(6800000.0, p.GetX());
	ASSERT_DOUBLE_EQ(0.0, p.GetY());
	ASSERT_DOUBLE_EQ(0.0, p.GetZ());
}

TEST(StateVector, ApogeeVector)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 9000.0, 0.0), IO::Astrodynamics::Time::TDB(663724800.00001490s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());

	auto p = sv.GetApogeeVector();

	ASSERT_DOUBLE_EQ(15200595.625908965, p.Magnitude());
	ASSERT_DOUBLE_EQ(-15200595.625908965, p.GetX());
	ASSERT_DOUBLE_EQ(0.0, p.GetY());
	ASSERT_DOUBLE_EQ(0.0, p.GetZ());
}

TEST(StateVector, FromTrueAnomaly)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 9000.0, 0.0), IO::Astrodynamics::Time::TDB(0.0s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());

	auto newSv = sv.ToStateVector(1.57);

	ASSERT_DOUBLE_EQ(1.57, newSv.GetPosition().Normalize().GetAngle(newSv.GetPerigeeVector().Normalize()));

	newSv = sv.ToStateVector(IO::Astrodynamics::Constants::PI);

	ASSERT_DOUBLE_EQ(IO::Astrodynamics::Constants::PI, newSv.GetPosition().Normalize().GetAngle(newSv.GetPerigeeVector().Normalize()));

	newSv = sv.ToStateVector(IO::Astrodynamics::Constants::PI + IO::Astrodynamics::Constants::PI2);
	ASSERT_DOUBLE_EQ(IO::Astrodynamics::Constants::PI2, newSv.GetPosition().Normalize().GetAngle(newSv.GetPerigeeVector().Normalize()));
}

TEST(StateVector, ToFrame)
{
	auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::Astrodynamics::OrbitalParameters::StateVector sv(sun, IO::Astrodynamics::Math::Vector3D(-2.649903367743050E+10,1.327574173383451E+11, 5.755671847054072E+10), IO::Astrodynamics::Math::Vector3D(-2.979426007043741E+04, -5.018052308799903E+03, -2.175393802830554E+03), IO::Astrodynamics::Time::TDB(0.0s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());

	auto nStateVector = sv.ToFrame(IO::Astrodynamics::Frames::InertialFrames::EclipticJ2000());

	ASSERT_DOUBLE_EQ(-2.649903367743050E+10,nStateVector.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(1.446972967925493E+11,nStateVector.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(-6.1114942597961426E+05,nStateVector.GetPosition().GetZ());
	ASSERT_DOUBLE_EQ(-2.979426007043741E+04,nStateVector.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(-5.469294939770602E+03,nStateVector.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(1.8178367850282484E-01,nStateVector.GetVelocity().GetZ());
}

TEST(StateVector, GetAscendingNodeVector)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::Astrodynamics::OrbitalParameters::StateVector sv(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0), IO::Astrodynamics::Math::Vector3D(0.0, 9000.0, 1000.0), IO::Astrodynamics::Time::TDB(0.0s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());

	auto anv = sv.GetAscendingNodeVector();

	ASSERT_DOUBLE_EQ(0.9999999739774097,anv.GetX());
	ASSERT_DOUBLE_EQ(0.00022673879821807146,anv.GetY());
	ASSERT_DOUBLE_EQ(2.5193199802008394e-05,anv.GetZ());
}