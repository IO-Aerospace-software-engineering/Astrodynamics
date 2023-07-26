#include <gtest/gtest.h>
#include <EphemerisKernel.h>
#include <filesystem>
#include <vector>
#include <StateVector.h>
#include <TDB.h>
#include <Spacecraft.h>
#include "TestsConstants.h"
#include "TestParameters.h"
#include <Aberrations.h>
#include <InertialFrames.h>
#include <CelestialBody.h>
#include <memory>

using namespace std::chrono_literals;
TEST(EphemerisKernel, WriteEvenlySpacedData)
{

	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::Body::Spacecraft::Spacecraft iss(-34, "ISS", 1.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));
	IO::Astrodynamics::OrbitalParameters::StateVector sv1(earth, IO::Astrodynamics::Math::Vector3D{5.314354587795519E+06, 3.155847941008321E+06, 2.822346477531172E+06}, IO::Astrodynamics::Math::Vector3D{-4.672670954754818E+03, 3.299429157421530E+03, 5.095794593488111E+03}, IO::Astrodynamics::Time::TDB(626417577.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv2(earth, {1.549954329309747E+06, 4.293503281635832E+06, 5.028588262180220E+06}, {-7.392939450028063E+03, 3.477943951618910E+02, 1.975839635187658E+03}, IO::Astrodynamics::Time::TDB(626418177.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv3(earth, {-2.896454033701685E+06, 3.542462012835863E+06, 5.016706976013824E+06}, {-6.859533085983158E+03, -2.755493626717945E+03, -2.014315631800109E+03}, IO::Astrodynamics::Time::TDB(626418777.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv4(earth, {-6.067539550053780E+06, 1.232431673036192E+06, 2.790829351763826E+06}, {-3.303131089221453E+03, -4.649328429479942E+03, -5.120408159473961E+03}, IO::Astrodynamics::Time::TDB(626419377.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv5(earth, {-6.563060493594203E+06, -1.620694354914843E+06, -6.686255917855799E+05}, {1.715272858993747E+03, -4.494999424926766E+03, -5.966010139168663E+03}, IO::Astrodynamics::Time::TDB(626419977.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv6(earth, {-4.164043911247631E+06, -3.759318199666532E+06, -3.832720991152593E+06}, {5.972111804776327E+03, -2.360655620937066E+03, -4.175153894575193E+03}, IO::Astrodynamics::Time::TDB(626420577.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv7(earth, {6.797277129039097E+04, -4.244124430692066E+06, -5.306286538854225E+06}, {7.593158397309122E+03, 8.052537216112745E+02, -5.494145941903844E+02}, IO::Astrodynamics::Time::TDB(626421177.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv8(earth, {4.270491162042410E+06, -2.865435312472839E+06, -4.443628659923305E+06}, {5.878548089282711E+03, 3.614706201375017E+03, 3.314943902644025E+03}, IO::Astrodynamics::Time::TDB(626421777.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv9(earth, {6.596527926846848E+06, -2.282558581529159E+05, -1.623786084927301E+06}, {1.575739453772276E+03, 4.839146422881710E+03, 5.723844038649382E+03}, IO::Astrodynamics::Time::TDB(626422377.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv10(earth, {6.018903384887210E+06, 2.509035785364610E+06, 1.912315011562237E+06}, {-3.427657631033425E+03, 3.934463471348762E+03, 5.608912859898233E+03}, IO::Astrodynamics::Time::TDB(626422977.7641s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());

	std::vector<IO::Astrodynamics::OrbitalParameters::StateVector> v;
	v.push_back(sv1);
	v.push_back(sv2);
	v.push_back(sv3);
	v.push_back(sv4);
	v.push_back(sv5);
	v.push_back(sv6);
	v.push_back(sv7);
	v.push_back(sv8);
	v.push_back(sv9);
	v.push_back(sv10);

	iss.WriteEphemeris(v);

	auto svStart = iss.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB(626417577.7641s), *earth);

	ASSERT_NEAR(5.314354587795519E+06, svStart.GetPosition().GetX(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(3.155847941008321E+06, svStart.GetPosition().GetY(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(2.822346477531172E+06, svStart.GetPosition().GetZ(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(-4.672670954754818E+03, svStart.GetVelocity().GetX(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);
	ASSERT_NEAR(3.299429157421530E+03, svStart.GetVelocity().GetY(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);
	ASSERT_NEAR(5.095794593488111E+03, svStart.GetVelocity().GetZ(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);

	auto svEnd = iss.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB(626421177.7641s), *earth);

	ASSERT_NEAR(6.797277129039097E+04, svEnd.GetPosition().GetX(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(-4.244124430692066E+06, svEnd.GetPosition().GetY(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(-5.306286538854225E+06, svEnd.GetPosition().GetZ(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(7.593158397309122E+03, svEnd.GetVelocity().GetX(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);
	ASSERT_NEAR(8.052537216112745E+02, svEnd.GetVelocity().GetY(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);
	ASSERT_NEAR(-5.494145941903844E+02, svEnd.GetVelocity().GetZ(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);

	auto svInterpolated = iss.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB(626421000.000000s), *earth); //"2019-11-07 17:50:00.0 TDB"

	ASSERT_NEAR(-1.274181283920850E+06, svInterpolated.GetPosition().GetX(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(-4.301645045480280E+06, svInterpolated.GetPosition().GetY(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(-5.103108831484487E+06, svInterpolated.GetPosition().GetZ(), IO::Astrodynamics::Test::Constants::DISTANCE_ACCURACY);
	ASSERT_NEAR(7.456849062509065E+03, svInterpolated.GetVelocity().GetX(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);
	ASSERT_NEAR(-1.603316800166004E+02, svInterpolated.GetVelocity().GetY(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);
	ASSERT_NEAR(-1.728953017174353E+03, svInterpolated.GetVelocity().GetZ(), IO::Astrodynamics::Test::Constants::VELOCITY_ACCURACY);
}
TEST(EphemerisKernel, GetCoverage)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::Body::Spacecraft::Spacecraft iss(-34, "ISS", 1.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));

	IO::Astrodynamics::OrbitalParameters::StateVector sv1(earth, IO::Astrodynamics::Math::Vector3D{5.314354587795519E+03, 3.155847941008321E+03, 2.822346477531172E+03}, IO::Astrodynamics::Math::Vector3D{-4.672670954754818E+00, 3.299429157421530E+00, 5.095794593488111E+00}, IO::Astrodynamics::Time::TDB(626417577.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv2(earth, {1.549954329309747E+03, 4.293503281635832E+03, 5.028588262180220E+03}, {-7.392939450028063E+00, 3.477943951618910E-01, 1.975839635187658E+00}, IO::Astrodynamics::Time::TDB(626418177.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv3(earth, {-2.896454033701685E+03, 3.542462012835863E+03, 5.016706976013824E+03}, {-6.859533085983158E+00, -2.755493626717945E+00, -2.014315631800109E+00}, IO::Astrodynamics::Time::TDB(626418777.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv4(earth, {-6.067539550053780E+03, 1.232431673036192E+03, 2.790829351763826E+03}, {-3.303131089221453E+00, -4.649328429479942E+00, -5.120408159473961E+00}, IO::Astrodynamics::Time::TDB(626419377.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv5(earth, {-6.563060493594203E+03, -1.620694354914843E+03, -6.686255917855799E+02}, {1.715272858993747E+00, -4.494999424926766E+00, -5.966010139168663E+00}, IO::Astrodynamics::Time::TDB(626419977.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv6(earth, {-4.164043911247631E+03, -3.759318199666532E+03, -3.832720991152593E+03}, {5.972111804776327E+00, -2.360655620937066E+00, -4.175153894575193E+00}, IO::Astrodynamics::Time::TDB(626420577.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv7(earth, {6.797277129039097E+01, -4.244124430692066E+03, -5.306286538854225E+03}, {7.593158397309122E+00, 8.052537216112745E-01, -5.494145941903844E-01}, IO::Astrodynamics::Time::TDB(626421177.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv8(earth, {4.270491162042410E+03, -2.865435312472839E+03, -4.443628659923305E+03}, {5.878548089282711E+00, 3.614706201375017E+00, 3.314943902644025E+00}, IO::Astrodynamics::Time::TDB(626421777.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv9(earth, {6.596527926846848E+03, -2.282558581529159E+02, -1.623786084927301E+03}, {1.575739453772276E+00, 4.839146422881710E+00, 5.723844038649382E+00}, IO::Astrodynamics::Time::TDB(626422377.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv10(earth, {6.018903384887210E+03, 2.509035785364610E+03, 1.912315011562237E+03}, {-3.427657631033425E+00, 3.934463471348762E+00, 5.608912859898233E+00}, IO::Astrodynamics::Time::TDB(626422977.7641s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());

	std::vector<IO::Astrodynamics::OrbitalParameters::StateVector> v;
	v.push_back(sv1);
	v.push_back(sv2);
	v.push_back(sv3);
	v.push_back(sv4);
	v.push_back(sv5);
	v.push_back(sv6);
	v.push_back(sv7);
	v.push_back(sv8);
	v.push_back(sv9);
	v.push_back(sv10);

	iss.WriteEphemeris(v);
	auto coverage = iss.GetEphemerisCoverageWindow();
	ASSERT_DOUBLE_EQ(5400.0, coverage.GetLength().GetSeconds().count());
	ASSERT_DOUBLE_EQ(626417577.7641, coverage.GetStartDate().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(626422977.7641, coverage.GetEndDate().GetSecondsFromJ2000().count());
}
TEST(EphemerisKernel, AddComment)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::Body::Spacecraft::Spacecraft iss(-34, "ISS", 1.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));

	IO::Astrodynamics::OrbitalParameters::StateVector sv1(earth, IO::Astrodynamics::Math::Vector3D{5.314354587795519E+03, 3.155847941008321E+03, 2.822346477531172E+03}, IO::Astrodynamics::Math::Vector3D{-4.672670954754818E+00, 3.299429157421530E+00, 5.095794593488111E+00}, IO::Astrodynamics::Time::TDB(626417577.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv2(earth, {1.549954329309747E+03, 4.293503281635832E+03, 5.028588262180220E+03}, {-7.392939450028063E+00, 3.477943951618910E-01, 1.975839635187658E+00}, IO::Astrodynamics::Time::TDB(626418177.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv3(earth, {-2.896454033701685E+03, 3.542462012835863E+03, 5.016706976013824E+03}, {-6.859533085983158E+00, -2.755493626717945E+00, -2.014315631800109E+00}, IO::Astrodynamics::Time::TDB(626418777.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv4(earth, {-6.067539550053780E+03, 1.232431673036192E+03, 2.790829351763826E+03}, {-3.303131089221453E+00, -4.649328429479942E+00, -5.120408159473961E+00}, IO::Astrodynamics::Time::TDB(626419377.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv5(earth, {-6.563060493594203E+03, -1.620694354914843E+03, -6.686255917855799E+02}, {1.715272858993747E+00, -4.494999424926766E+00, -5.966010139168663E+00}, IO::Astrodynamics::Time::TDB(626419977.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv6(earth, {-4.164043911247631E+03, -3.759318199666532E+03, -3.832720991152593E+03}, {5.972111804776327E+00, -2.360655620937066E+00, -4.175153894575193E+00}, IO::Astrodynamics::Time::TDB(626420577.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv7(earth, {6.797277129039097E+01, -4.244124430692066E+03, -5.306286538854225E+03}, {7.593158397309122E+00, 8.052537216112745E-01, -5.494145941903844E-01}, IO::Astrodynamics::Time::TDB(626421177.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv8(earth, {4.270491162042410E+03, -2.865435312472839E+03, -4.443628659923305E+03}, {5.878548089282711E+00, 3.614706201375017E+00, 3.314943902644025E+00}, IO::Astrodynamics::Time::TDB(626421777.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv9(earth, {6.596527926846848E+03, -2.282558581529159E+02, -1.623786084927301E+03}, {1.575739453772276E+00, 4.839146422881710E+00, 5.723844038649382E+00}, IO::Astrodynamics::Time::TDB(626422377.7641s),
                                                          IO::Astrodynamics::Frames::InertialFrames::ICRF());

	IO::Astrodynamics::OrbitalParameters::StateVector sv10(earth, {6.018903384887210E+03, 2.509035785364610E+03, 1.912315011562237E+03}, {-3.427657631033425E+00, 3.934463471348762E+00, 5.608912859898233E+00}, IO::Astrodynamics::Time::TDB(626422977.7641s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());

	std::vector<IO::Astrodynamics::OrbitalParameters::StateVector> v;
	v.push_back(sv1);
	v.push_back(sv2);
	v.push_back(sv3);
	v.push_back(sv4);
	v.push_back(sv5);
	v.push_back(sv6);
	v.push_back(sv7);
	v.push_back(sv8);
	v.push_back(sv9);
	v.push_back(sv10);

	iss.WriteEphemeris(v);
	iss.WriteEphemerisKernelComment("Comment Test");
	auto comment = iss.ReadEphemerisKernelComment();
	ASSERT_STREQ("Comment Test", comment.c_str());
}
TEST(EphemerisKernel, AddTooLongComment)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::Body::Spacecraft::Spacecraft iss(-34, "ISS", 1.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams));

	ASSERT_THROW(iss.WriteEphemerisKernelComment("This is a big message which exceed the maximum chars allowed-This is a big message which exceed the maximum chars allowed"), IO::Astrodynamics::Exception::SDKException);
}
