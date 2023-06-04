#include <gtest/gtest.h>
#include <StateVector.h>
#include <InertialFrames.h>
#include <Constants.h>

using namespace std::chrono_literals;
TEST(StateVector, Initialization)
{
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(1);
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	ASSERT_DOUBLE_EQ(1.0, sv.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(2.0, sv.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(3.0, sv.GetPosition().GetZ());

	ASSERT_DOUBLE_EQ(4.0, sv.GetVelocity().GetX());
	ASSERT_EQ(5.0, sv.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(6.0, sv.GetVelocity().GetZ());

	ASSERT_DOUBLE_EQ(100.0, sv.GetEpoch().GetSecondsFromJ2000().count());
	ASSERT_EQ(earth.get(), sv.GetCenterOfMotion().get());

	double state[6] = {11.0, 12.0, 13.0, 14.0, 15.0, 16.0};
	IO::SDK::OrbitalParameters::StateVector svFromState(earth, state, IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());

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
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);
	IO::SDK::OrbitalParameters::StateVector sv(sun, IO::SDK::Math::Vector3D(149.6e9, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 2.98e4, 0.0), IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());
	ASSERT_DOUBLE_EQ(4458080000000000.0, sv.GetSpecificAngularMomentum().Magnitude()); //Earth momentum
}

TEST(StateVector, GetSpecificOrbitalEnergy)
{
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());
	ASSERT_DOUBLE_EQ(-29305465.588067468, sv.GetSpecificOrbitalEnergy()); //Earth momentum
}

TEST(StateVector, GetSemiMajorAxis)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(6.800799983064672E+06, sv.GetSemiMajorAxis(), 1e2);
}

TEST(StateVector, GetEccentricity)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(1.352296372623237E-03, sv.GetEccentricity(), 1e-5);
}

TEST(StateVector, GetInclination)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(5.171933849805028E+01 * IO::SDK::Constants::DEG_RAD, sv.GetInclination(), 1e-4);
}

TEST(StateVector, GetPeriapsisArgument)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(1.062027499375132E+02 * IO::SDK::Constants::DEG_RAD, sv.GetPeriapsisArgument(), 1e-2);
}

TEST(StateVector, GetRAN)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(3.257645785453723E+01 * IO::SDK::Constants::DEG_RAD, sv.GetRightAscendingNodeLongitude(), 1e-2);
}

TEST(StateVector, GetMeanAnomaly)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(4.546651762339189E+01 * IO::SDK::Constants::DEG_RAD, sv.GetMeanAnomaly(), 1e-2);
}

TEST(StateVector, GetTrueAnomaly)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(4.557711170488279E+01 * IO::SDK::Constants::DEG_RAD, sv.GetTrueAnomaly(), 1e-2);
}

TEST(StateVector, GetPeriod)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(5.581500745616037E+03, sv.GetPeriod().GetSeconds().count(), 1);
}

TEST(StateVector, GetMeanMotion)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_NEAR(6.448974542312881E-02 * IO::SDK::Constants::DEG_RAD, sv.GetMeanMotion(), 1e-6);
}

TEST(StateVector, IsElliptical)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(-6.116559469556896E+06, -1.546174698676721E+06, 2.521950157430313E+06), IO::SDK::Math::Vector3D(-8.078523150700097E+02, -5.477647950892673E+03, -5.297615757935174E+03), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_TRUE(sv.IsElliptical());
}

TEST(StateVector, IsHyperbolic)
{
	//ISS
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 18000.0, 0.0), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_TRUE(sv.IsHyperbolic());
}

TEST(StateVector, IsParabolic)
{
	//FICTIVE
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	double escapeVelocity = std::sqrt((earth->GetMu() * 2.0) / 6800000.0);

	IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, escapeVelocity, 0.0), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_TRUE(sv.IsParabolic());
}

TEST(StateVector, CheckUpdateCenterOfMotionToParentBody)
{
	//FICTIVE
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);			//GEOPHYSICAL PROPERTIES provided by JPL
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun); //GEOPHYSICAL PROPERTIES provided by JPL
	auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth); //GEOPHYSICAL PROPERTIES provided by JPL

	//Define SV with earth as center of motion but the influence body is the sun in this case at 2021-01-01 00:00:00.000000 TDB
	IO::SDK::OrbitalParameters::StateVector sv{earth, IO::SDK::Math::Vector3D(2000000000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 3000.0, 0.0), IO::SDK::Time::TDB(662731200.000000s), IO::SDK::Frames::InertialFrames::GetICRF()};
	auto newSV = sv.CheckAndUpdateCenterOfMotion();

	//Low accuracy due to conical propagation
	ASSERT_EQ(10, newSV.GetCenterOfMotion()->GetId());
	ASSERT_DOUBLE_EQ(-2.4795375379297768E+10, newSV.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(1.3270111352322429E+11, newSV.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(5.7525334752378304E+10, newSV.GetPosition().GetZ());

	ASSERT_DOUBLE_EQ(-2.9765580095900841E+04, newSV.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(-2.0753399173890839E+03, newSV.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(-2.2009299676732885E+03, newSV.GetVelocity().GetZ());
}

TEST(StateVector, CheckUpdateCenterOfMotionToSatelliteBody)
{
	//FICTIVE
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);			//GEOPHYSICAL PROPERTIES provided by JPL
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun); //GEOPHYSICAL PROPERTIES provided by JPL
	auto moon = std::make_shared<IO::SDK::Body::CelestialBody>(301, earth); //GEOPHYSICAL PROPERTIES provided by JPL

	//Define SV with earth as center of motion but the influence body is the moon in this case at 2021-01-01 00:00:00.000000 TDB
	IO::SDK::OrbitalParameters::StateVector sv{earth, IO::SDK::Math::Vector3D(-2.088864826237993E+08, 2.911146390982051E+08, 1.515746884380044E+08), IO::SDK::Math::Vector3D(-8.366764389833921E+02, -5.602543663174073E+02, -1.710459390585548E+02), IO::SDK::Time::TDB(662731200.000000s), IO::SDK::Frames::InertialFrames::GetICRF()};
	auto newSV = sv.CheckAndUpdateCenterOfMotion();

	//Low accuracy due to conical propagation
	ASSERT_EQ(301, newSV.GetCenterOfMotion()->GetId());
	ASSERT_DOUBLE_EQ(-1.9999993200141788E06, newSV.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(2.0000003739118576E06, newSV.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(0.14124882221221924, newSV.GetPosition().GetZ());

	ASSERT_DOUBLE_EQ(-8.8791193775250576e-07, newSV.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(5.0141545671067433e-07, newSV.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(3.3448974932070996e-06, newSV.GetVelocity().GetZ());
}

TEST(StateVector, Assignement)
{
	//FICTIVE
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10);			//GEOPHYSICAL PROPERTIES provided by JPL
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, sun); //GEOPHYSICAL PROPERTIES provided by JPL

	//Define SV with earth as center of motion but the influence body is the moon in this case at 2021-01-01 00:00:00.000000 TDB
	IO::SDK::OrbitalParameters::StateVector sv{earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF()};
	IO::SDK::OrbitalParameters::StateVector sv2{earth, IO::SDK::Math::Vector3D(10.0, 20.0, 30.0), IO::SDK::Math::Vector3D(40.0, 50.0, 60.0), IO::SDK::Time::TDB(1000.0s), IO::SDK::Frames::InertialFrames::GetICRF()};
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
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 18000.0, 0.0), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	//Low accuracy due to conical propagation
	ASSERT_EQ(IO::SDK::Frames::InertialFrames::GetICRF(), sv.GetFrame());
	ASSERT_NE(IO::SDK::Frames::InertialFrames::Galactic(), sv.GetFrame());
}

TEST(StateVector, EccentricityVector)
{
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 9000.0, 0.0), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	auto e = sv.GetEccentricityVector();

	ASSERT_DOUBLE_EQ(sv.GetEccentricity(), e.Magnitude());
	ASSERT_DOUBLE_EQ(1.0, e.Normalize().GetX());
	ASSERT_DOUBLE_EQ(0.0, e.Normalize().GetY());
	ASSERT_DOUBLE_EQ(0.0, e.Normalize().GetZ());
}

TEST(StateVector, PerigeeVector)
{
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 9000.0, 0.0), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	auto p = sv.GetPerigeeVector();

	ASSERT_DOUBLE_EQ(6800000.0, p.Magnitude());
	ASSERT_DOUBLE_EQ(6800000.0, p.GetX());
	ASSERT_DOUBLE_EQ(0.0, p.GetY());
	ASSERT_DOUBLE_EQ(0.0, p.GetZ());
}

TEST(StateVector, ApogeeVector)
{
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 9000.0, 0.0), IO::SDK::Time::TDB(663724800.00001490s), IO::SDK::Frames::InertialFrames::GetICRF());

	auto p = sv.GetApogeeVector();

	ASSERT_DOUBLE_EQ(15200595.625908965, p.Magnitude());
	ASSERT_DOUBLE_EQ(-15200595.625908965, p.GetX());
	ASSERT_DOUBLE_EQ(0.0, p.GetY());
	ASSERT_DOUBLE_EQ(0.0, p.GetZ());
}

TEST(StateVector, FromTrueAnomaly)
{
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 9000.0, 0.0), IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

	auto newSv = sv.ToStateVector(1.57);

	ASSERT_DOUBLE_EQ(1.57, newSv.GetPosition().Normalize().GetAngle(newSv.GetPerigeeVector().Normalize()));

	newSv = sv.ToStateVector(IO::SDK::Constants::PI);

	ASSERT_DOUBLE_EQ(IO::SDK::Constants::PI, newSv.GetPosition().Normalize().GetAngle(newSv.GetPerigeeVector().Normalize()));

	newSv = sv.ToStateVector(IO::SDK::Constants::PI + IO::SDK::Constants::PI2);
	ASSERT_DOUBLE_EQ(IO::SDK::Constants::PI2, newSv.GetPosition().Normalize().GetAngle(newSv.GetPerigeeVector().Normalize()));
}

TEST(StateVector, ToFrame)
{
	auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::SDK::OrbitalParameters::StateVector sv(sun, IO::SDK::Math::Vector3D(-2.649903367743050E+10,1.327574173383451E+11, 5.755671847054072E+10), IO::SDK::Math::Vector3D(-2.979426007043741E+04, -5.018052308799903E+03, -2.175393802830554E+03), IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

	auto nStateVector = sv.ToFrame(IO::SDK::Frames::InertialFrames::Ecliptic());

	ASSERT_DOUBLE_EQ(-2.649903367743050E+10,nStateVector.GetPosition().GetX());
	ASSERT_DOUBLE_EQ(1.446972967925493E+11,nStateVector.GetPosition().GetY());
	ASSERT_DOUBLE_EQ(-6.1114942597961426E+05,nStateVector.GetPosition().GetZ());
	ASSERT_DOUBLE_EQ(-2.979426007043741E+04,nStateVector.GetVelocity().GetX());
	ASSERT_DOUBLE_EQ(-5.469294939770602E+03,nStateVector.GetVelocity().GetY());
	ASSERT_DOUBLE_EQ(1.8178367850282484E-01,nStateVector.GetVelocity().GetZ());
}

TEST(StateVector, GetAscendingNodeVector)
{
	auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399); //GEOPHYSICAL PROPERTIES provided by JPL
	const IO::SDK::OrbitalParameters::StateVector sv(earth, IO::SDK::Math::Vector3D(6800000.0, 0.0, 0.0), IO::SDK::Math::Vector3D(0.0, 9000.0, 1000.0), IO::SDK::Time::TDB(0.0s), IO::SDK::Frames::InertialFrames::GetICRF());

	auto anv = sv.GetAscendingNodeVector();

	ASSERT_DOUBLE_EQ(1.0,anv.GetX());
	ASSERT_DOUBLE_EQ(0.0,anv.GetY());
	ASSERT_DOUBLE_EQ(0.0,anv.GetZ());
}