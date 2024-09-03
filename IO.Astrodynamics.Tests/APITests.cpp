/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include<gtest/gtest.h>
#include "TestParameters.h"
#include "Proxy.h"
#include "InertialFrames.h"
#include "Spacecraft.h"
#include "InvalidArgumentException.h"
#include <Converters.cpp>

TEST(API, TDBToString)
{
    auto res = TDBToStringProxy(0.0);
    ASSERT_STREQ("2000-01-01 12:00:00.000000 (TDB)", res);
    free((void*)res);
}

TEST(API, UTCToString)
{
    auto res = UTCToStringProxy(0.0);
    ASSERT_STREQ("2000-01-01 12:00:00.000000 (UTC)", res);
    free((void*)res);
}

TEST(API, FindWindowsOnCoordinateConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 730036800.0;
    searchWindow.end = 730123200;
    FindWindowsOnCoordinateConstraintProxy(searchWindow, 399013, 301, "DSS-13_TOPO", "LATITUDINAL", "LATITUDE", ">",
                                           0.0, 0.0, "NONE", 60.0,
                                           windows);

    ASSERT_STREQ("2023-02-19 14:33:08.918098 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2023-02-19 23:58:50.814787 (UTC)", ToTDBWindow(windows[0]).GetEndDate().ToUTC().ToString().c_str());
}

TEST(API, FindWindowsOnDistanceConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::Astrodynamics::Time::TDB("2007 JAN 1").GetSecondsFromJ2000().count();
    searchWindow.end = IO::Astrodynamics::Time::TDB("2007 APR 1").GetSecondsFromJ2000().count();
    FindWindowsOnDistanceConstraintProxy(searchWindow, 399, 301, ">", 400000000, "NONE", 86400.0, windows);

    ASSERT_STREQ("2007-01-08 00:11:07.628591 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-01-13 06:37:47.948144 (TDB)", ToTDBWindow(windows[0]).GetEndDate().ToString().c_str());
    ASSERT_STREQ("2007-03-29 22:53:58.151896 (TDB)", ToTDBWindow(windows[3]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-04-01 00:01:05.185654 (TDB)", ToTDBWindow(windows[3]).GetEndDate().ToString().c_str());
}

TEST(API, FindWindowsOnIlluminationConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 TDB").GetSecondsFromJ2000().count();
    searchWindow.end = IO::Astrodynamics::Time::TDB("2021-05-18 12:00:00 TDB").GetSecondsFromJ2000().count();
    IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD,
                                                          48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0);
    FindWindowsOnIlluminationConstraintProxy(searchWindow, 10, "SUN", 399, "IAU_EARTH",
                                             geodetic, "INCIDENCE", "<",
                                             IO::Astrodynamics::Constants::PI2 -
                                             IO::Astrodynamics::Constants::OfficialTwilight, 0.0,
                                             "CN+S", 4.5 * 60 * 60, "Ellipsoid", windows);

    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:33.699813 (UTC)", ToTDBWindow(windows[0]).GetEndDate().ToUTC().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", ToTDBWindow(windows[1]).GetStartDate().ToUTC().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", ToTDBWindow(windows[1]).GetEndDate().ToString().c_str());
}

TEST(API, FindWindowsOnOccultationConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::Astrodynamics::Time::TDB("2001 DEC 13").GetSecondsFromJ2000().count();
    searchWindow.end = IO::Astrodynamics::Time::TDB("2001 DEC 15").GetSecondsFromJ2000().count();
    FindWindowsOnOccultationConstraintProxy(searchWindow, 399, 10, "IAU_SUN", "ELLIPSOID", 301, "IAU_MOON", "ELLIPSOID",
                                            "ANY", "LT", 3600.0, windows);

    ASSERT_STREQ("2001-12-14 20:10:15.410588 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2001-12-14 21:35:49.100520 (TDB)", ToTDBWindow(windows[0]).GetEndDate().ToString().c_str());
}


TEST(API, ReadEphemerisProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 0.0;
    searchWindow.end = 100.0;

    IO::Astrodynamics::API::DTO::StateVectorDTO sv[5000];
    ReadEphemerisProxy(searchWindow, 399, 301, "J2000", "LT", 10.0, sv);
    ASSERT_DOUBLE_EQ(-291569264.48965073, sv[0].position.x);
    ASSERT_DOUBLE_EQ(-266709187.1624887, sv[0].position.y);
    ASSERT_DOUBLE_EQ(-76099155.244104564, sv[0].position.z);
    ASSERT_DOUBLE_EQ(643.53061483971885, sv[0].velocity.x);
    ASSERT_DOUBLE_EQ(-666.08181440799092, sv[0].velocity.y);
    ASSERT_DOUBLE_EQ(-301.32283209101018, sv[0].velocity.z);
    ASSERT_DOUBLE_EQ(399, sv[0].centerOfMotionId);
    ASSERT_STREQ("J2000", sv[0].inertialFrame);
    ASSERT_DOUBLE_EQ(0.0, sv[0].epoch);
}

TEST(API, ReadEphemerisProxyException)
{
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 0.0;
    searchWindow.end = 10001.0;

    IO::Astrodynamics::API::DTO::StateVectorDTO sv[5000];
    ASSERT_THROW(ReadEphemerisProxy(searchWindow, 399, 301, "J2000", "LT", 1.0, sv),
                 IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(API, ReadSpacecraftOrientationProxyException)
{
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 0.0;
    searchWindow.end = 10001.0;

    IO::Astrodynamics::API::DTO::StateOrientationDTO so[10000];
    ASSERT_THROW(ReadOrientationProxy(searchWindow, -172, 10.0 * std::pow(2.0, ClockAccuracy), "J2000", 1.0, so),
                 IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(API, ToTDB)
{
    ASSERT_DOUBLE_EQ(64.183927284669423, ConvertUTCToTDBProxy(0.0));
}

TEST(API, ToUTC)
{
    ASSERT_DOUBLE_EQ(-64.183927263223808, ConvertTDBToUTCProxy(0.0));
}

TEST(API, Version)
{
    const char* res = GetSpiceVersionProxy();
    ASSERT_STREQ("CSPICE_N0067", res);
    free((void*)res);
}

TEST(API, WriteEphemeris)
{
    const int size = 10;
    IO::Astrodynamics::API::DTO::StateVectorDTO sv[size];
    for (int i = 0; i < size; ++i)
    {
        sv[i].position.x = 6800 + i;
        sv[i].position.y = i;
        sv[i].position.z = i;
        sv[i].velocity.x = i;
        sv[i].velocity.y = 8.0 + i * 0.001;
        sv[i].velocity.z = i;
        sv[i].epoch = i;
        sv[i].centerOfMotionId = 399;
        sv[i].SetFrame("J2000");
    }

    //Write ephemeris file
    WriteEphemerisProxy("EphemerisTestFile.spk", -135, sv, size);

    //Load ephemeris file
    LoadKernelsProxy("EphemerisTestFile.spk");

    IO::Astrodynamics::API::DTO::WindowDTO window{};
    window.start = 0.0;
    window.end = 9.0;
    IO::Astrodynamics::API::DTO::StateVectorDTO svresult[size];
    ReadEphemerisProxy(window, 399, -135, "J2000", "NONE", 1.0, svresult);
    for (int i = 0; i < size; ++i)
    {
        ASSERT_DOUBLE_EQ(svresult[i].position.x, 6800 + i);
        ASSERT_DOUBLE_EQ(svresult[i].position.y, i);
        ASSERT_DOUBLE_EQ(svresult[i].position.z, i);
        ASSERT_DOUBLE_EQ(svresult[i].velocity.x, i);
        ASSERT_DOUBLE_EQ(svresult[i].velocity.y, 8 + i * 0.001);
        ASSERT_DOUBLE_EQ(svresult[i].velocity.z, i);
        ASSERT_DOUBLE_EQ(svresult[i].epoch, i);
        ASSERT_EQ(svresult[i].centerOfMotionId, 399);
        ASSERT_STREQ(svresult[i].inertialFrame, "J2000");
    }
}

TEST(API, WriteOrientation)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                           IO::Astrodynamics::Math::Vector3D(
                                                               6800000.0,
                                                               0.0, 0.0),
                                                           IO::Astrodynamics::Math::Vector3D(
                                                               0.0,
                                                               8000.0,
                                                               0.0),
                                                           IO::Astrodynamics::Time::TDB(
                                                               0.0s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-175, "SPC000", 1000.0, 3000.0, std::string(SpacecraftPath),
                                                        std::move(orbitalParams));
    const int size = 10;
    IO::Astrodynamics::API::DTO::StateOrientationDTO sv[size];
    for (int i = 0; i < size; ++i)
    {
        sv[i].orientation.w = i;
        sv[i].orientation.x = 1 + i * 0.1;
        sv[i].orientation.y = 1 + i * 0.2;
        sv[i].orientation.z = 1 + i * 0.3;
        sv[i].angularVelocity.x = 0.0;
        sv[i].angularVelocity.y = 0.0;
        sv[i].angularVelocity.z = 0.0;
        sv[i].epoch = i;
        sv[i].SetFrame("J2000");
    }

    //Write ephemeris file
    WriteOrientationProxy((std::string(SpacecraftPath) + "/OrientationTestFile.ck").c_str(), -175, sv, size);

    //Load ephemeris file
    LoadKernelsProxy((std::string(SpacecraftPath) + "/OrientationTestFile.ck").c_str());

    IO::Astrodynamics::API::DTO::WindowDTO window{};
    window.start = 0.0;
    window.end = 9.0;
    IO::Astrodynamics::API::DTO::StateOrientationDTO soresult[size];
    ReadOrientationProxy(window, -175, 0.0, "J2000", 1.0, soresult);

    ASSERT_DOUBLE_EQ(soresult[0].orientation.w, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].orientation.x, -0.57735026918962573);
    ASSERT_DOUBLE_EQ(soresult[0].orientation.y, -0.57735026918962573);
    ASSERT_DOUBLE_EQ(soresult[0].orientation.z, -0.57735026918962573);
    ASSERT_DOUBLE_EQ(soresult[0].angularVelocity.x, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].angularVelocity.y, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].angularVelocity.z, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].epoch, 0);
    ASSERT_STREQ(soresult[0].frame, "J2000");

    ASSERT_DOUBLE_EQ(soresult[4].orientation.w, 0.78386180166962049);
    ASSERT_DOUBLE_EQ(soresult[4].orientation.x, 0.27435163058436718);
    ASSERT_DOUBLE_EQ(soresult[4].orientation.y, 0.35273781075132921);
    ASSERT_DOUBLE_EQ(soresult[4].orientation.z, 0.43112399091829129);
    ASSERT_DOUBLE_EQ(soresult[4].angularVelocity.x, 0.0);
    ASSERT_DOUBLE_EQ(soresult[4].angularVelocity.y, 0.0);
    ASSERT_DOUBLE_EQ(soresult[4].angularVelocity.z, 0.0);
    ASSERT_DOUBLE_EQ(soresult[4].epoch, 4);
    ASSERT_STREQ(soresult[4].frame, "J2000");

    ASSERT_DOUBLE_EQ(soresult[9].orientation.w, 0.87358057364767872);
    ASSERT_DOUBLE_EQ(soresult[9].orientation.x, 0.18442256554784328);
    ASSERT_DOUBLE_EQ(soresult[9].orientation.y, 0.27178062291261118);
    ASSERT_DOUBLE_EQ(soresult[9].orientation.z, 0.359138680277379);
    ASSERT_DOUBLE_EQ(soresult[9].angularVelocity.x, 0.0);
    ASSERT_DOUBLE_EQ(soresult[9].angularVelocity.y, 0.0);
    ASSERT_DOUBLE_EQ(soresult[9].angularVelocity.z, 0.0);
    ASSERT_DOUBLE_EQ(soresult[9].epoch, 9);
    ASSERT_STREQ(soresult[9].frame, "J2000");
}

TEST(API, GetBodyInformation)
{
    auto res = GetCelestialBodyInfoProxy(399);
    ASSERT_EQ(399, res.Id);
    ASSERT_EQ(10, res.CenterOfMotionId);
    ASSERT_STREQ("EARTH", res.Name);
    ASSERT_EQ(13000, res.FrameId);
    ASSERT_STREQ("ITRF93", res.FrameName);
    ASSERT_DOUBLE_EQ(398600435436095.94, res.GM);
    ASSERT_DOUBLE_EQ(6378136.5999999998, res.Radii.x);
    ASSERT_DOUBLE_EQ(6378136.5999999998, res.Radii.y);
    ASSERT_DOUBLE_EQ(6356751.9000000002, res.Radii.z);
    ASSERT_DOUBLE_EQ(0.001082616, res.J2);
    ASSERT_DOUBLE_EQ(-2.5388099999999996e-06, res.J3);
    ASSERT_DOUBLE_EQ(-1.6559699999999999e-06, res.J4);
}

TEST(API, GetBodyInformationWithoutJ)
{
    auto res = GetCelestialBodyInfoProxy(301);
    ASSERT_EQ(301, res.Id);
    ASSERT_EQ(399, res.CenterOfMotionId);
    ASSERT_STREQ("MOON", res.Name);
    ASSERT_EQ(31001, res.FrameId);
    ASSERT_STREQ("MOON_ME", res.FrameName);
    ASSERT_DOUBLE_EQ(4902800066163.7959, res.GM);
    ASSERT_DOUBLE_EQ(1737400, res.Radii.x);
    ASSERT_DOUBLE_EQ(1737400, res.Radii.y);
    ASSERT_DOUBLE_EQ(1737400, res.Radii.z);
    ASSERT_TRUE(std::isnan(res.J2));
    ASSERT_TRUE(std::isnan(res.J3));
    ASSERT_TRUE(std::isnan(res.J4));
}

TEST(API, GetBodyInformationInvalidId)
{
    auto res = GetCelestialBodyInfoProxy(398);
}

TEST(API, TransformFrame)
{
    auto res = TransformFrameProxy(IO::Astrodynamics::Frames::InertialFrames::ICRF().GetName().c_str(), "ITRF93", 0.0);
    ASSERT_DOUBLE_EQ(0.76713121189662548, res.Rotation.w);
    ASSERT_DOUBLE_EQ(-1.8618846012434252e-05, res.Rotation.x);
    ASSERT_DOUBLE_EQ(8.468919252183845e-07, res.Rotation.y);
    ASSERT_DOUBLE_EQ(0.64149022080358797, res.Rotation.z);
    ASSERT_DOUBLE_EQ(-1.9637714059853662e-09, res.AngularVelocity.x);
    ASSERT_DOUBLE_EQ(-2.0389340573814659e-09, res.AngularVelocity.y);
    ASSERT_DOUBLE_EQ(7.2921150642488516e-05, res.AngularVelocity.z);
}

TEST(API, ConvertTLEToStateVectorProxy)
{
    IO::Astrodynamics::Time::TDB epoch("2021-01-20T18:50:13.663106");
    auto stateVector = ConvertTLEToStateVectorProxy("ISS",
                                                    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                                                    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703",
                                                    epoch.GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(4363669.2613373389, stateVector.position.x);
    ASSERT_DOUBLE_EQ(-3627809.912410662, stateVector.position.y);
    ASSERT_DOUBLE_EQ(-3747415.4653566754, stateVector.position.z);
    ASSERT_DOUBLE_EQ(5805.8241824895995, stateVector.velocity.x);
    ASSERT_DOUBLE_EQ(2575.7226437161635, stateVector.velocity.y);
    ASSERT_DOUBLE_EQ(4271.5974622410786, stateVector.velocity.z);
    ASSERT_DOUBLE_EQ(664440682.84760022, stateVector.epoch);
}

TEST(API, GetTLEElementsProxy)
{
    auto res = GetTLEElementsProxy("ISS",
                                   "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                                   "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
    ASSERT_DOUBLE_EQ(6803376.2171725659, res.A);
    ASSERT_DOUBLE_EQ(4.9299999999999999e-05, res.E);
    ASSERT_DOUBLE_EQ(0.9013281683026676, res.I);
    ASSERT_DOUBLE_EQ(6.1615568022666061, res.O);
    ASSERT_DOUBLE_EQ(5.6003339639830649, res.W);
    ASSERT_DOUBLE_EQ(0.68479738531249512, res.M);
    ASSERT_DOUBLE_EQ(664419082.8475914, res.Epoch);
    ASSERT_DOUBLE_EQ(5.06539394194257e-10, res.BalisticCoefficient);
    ASSERT_DOUBLE_EQ(0.0001027, res.DragTerm);
    ASSERT_DOUBLE_EQ(0.0, res.SecondDerivativeOfMeanMotion);
}

TEST(API, ConvertConicOrbitalElementsToStateVector)
{
    double perifocalDist = std::sqrt(std::pow(-6.116559469556896E+06, 2) + std::pow(-1.546174698676721E+06, 2) +
        std::pow(2.521950157430313E+06, 2));

    IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conics;
    conics.SetFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray());
    conics.epoch = 663724800.00001490; //"2021-01-12T11:58:50.816" UTC
    conics.meanAnomaly = 4.541224977546975E+01 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.periapsisArgument = 1.062574316262159E+02 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.ascendingNodeLongitude = 3.257605322534260E+01 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.inclination = 5.171921958517460E+01 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.eccentricity = 1.353139738203394E-03;
    conics.perifocalDistance = perifocalDist;
    conics.centerOfMotionId = 399;

    auto sv = ConvertConicElementsToStateVectorProxy(conics);

    //Low accuracy due to conical propagation
    ASSERT_NEAR(-6.116559469556896E+06, sv.position.x, 3e3);
    ASSERT_NEAR(-1.546174698676721E+06, sv.position.y, 3e3);
    ASSERT_NEAR(2.521950157430313E+06, sv.position.z, 3e3);

    ASSERT_NEAR(-8.078523150700097E+02, sv.velocity.x, 0.2);
    ASSERT_NEAR(-5.477647950892673E+03, sv.velocity.y, 1.2);
    ASSERT_NEAR(-5.297615757935174E+03, sv.velocity.z, 1.1);
    ASSERT_EQ(663724800.00001490, sv.epoch);
    ASSERT_EQ(399, sv.centerOfMotionId);
    ASSERT_STREQ(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray(), sv.inertialFrame);
}

TEST(API, ConvertEquinoctialElementsToStateVector)
{
    //keplerian elements
    double p = 1.0e7;
    double ecc = 0.1;
    double a = p / (1. - ecc);
    double argp = 30.0 * rpd_c();
    double node = 15.0 * rpd_c();
    double inc = 10.0 * rpd_c();
    double m0 = 45.0 * rpd_c();
    IO::Astrodynamics::Time::TDB t0{-100000000.0s};

    //equinoctial elements
    double h = ecc * sin(argp + node);
    double k = ecc * cos(argp + node);
    double p2 = tan(inc / 2.) * sin(node);
    double q = tan(inc / 2.) * cos(node);
    double L = m0 + argp + node;

    IO::Astrodynamics::API::DTO::EquinoctialElementsDTO eqDTO;
    eqDTO.SetFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray());
    eqDTO.declinationOfThePole = IO::Astrodynamics::Constants::PI2;
    eqDTO.rightAscensionOfThePole = -IO::Astrodynamics::Constants::PI2;
    eqDTO.ascendingNodeLongitudeRate = 0.0;
    eqDTO.periapsisLongitudeRate = 0.0;
    eqDTO.h = h;
    eqDTO.p = p2;
    eqDTO.semiMajorAxis = a;
    eqDTO.epoch = t0.GetSecondsFromJ2000().count();
    eqDTO.centerOfMotionId = 399;
    eqDTO.L = L;
    eqDTO.k = k;
    eqDTO.q = q;

    auto sv = ConvertEquinoctialElementsToStateVectorProxy(eqDTO);

    ASSERT_DOUBLE_EQ(-1557343.2179623565, sv.position.x);
    ASSERT_DOUBLE_EQ(10112046.56492505, sv.position.y);
    ASSERT_DOUBLE_EQ(1793343.6111546031, sv.position.z);
    ASSERT_DOUBLE_EQ(-6369.0795341145204, sv.velocity.x);
    ASSERT_DOUBLE_EQ(-517.51239201161684, sv.velocity.y);
    ASSERT_DOUBLE_EQ(202.52220483204573, sv.velocity.z);
    ASSERT_EQ(399, sv.centerOfMotionId);
    ASSERT_STREQ(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray(), sv.inertialFrame);
}

TEST(API, ConvertToRaDec)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);
    auto sv = moon->GetOrbitalParametersAtEpoch()->ToStateVector();

    auto svDTO = ToStateVectorDTO(sv);
    auto ra = ConvertStateVectorToEquatorialCoordinatesProxy(svDTO);
    ASSERT_DOUBLE_EQ(222.44729949955743, ra.rightAscension * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-10.900186051699617, ra.declination * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(402448639.88732731, ra.range);
}

TEST(API, ConvertEllipticStateToConic)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    IO::Astrodynamics::API::DTO::StateVectorDTO stateVector;
    stateVector.epoch = 2451545.0; // J2000 epoch
    stateVector.position = {6800000.0, 0.0, 0.0};
    stateVector.velocity = {0.0, 8000.0, 0.0};
    stateVector.centerOfMotionId=earth->GetId();
    stateVector.SetFrame("J2000");

    auto result = ConvertStateVectorToConicOrbitalElementProxy(stateVector, earth->GetMu());

    auto stateVector2 = ConvertConicElementsToStateVectorAtEpochProxy(result,stateVector.epoch,earth->GetMu());
    EXPECT_NEAR(stateVector.position.x, stateVector2.position.x, 1e-6);
    EXPECT_NEAR(stateVector.position.y, stateVector2.position.y, 1e-6);
    EXPECT_NEAR(stateVector.position.z, stateVector2.position.z, 1e-6);
    EXPECT_NEAR(stateVector.velocity.x, stateVector2.velocity.x, 1e-6);
    EXPECT_NEAR(stateVector.velocity.y, stateVector2.velocity.y, 1e-6);
    EXPECT_NEAR(stateVector.velocity.z, stateVector2.velocity.z, 1e-6);
    EXPECT_NEAR(stateVector.epoch, stateVector2.epoch, 1e-6);
}

TEST(API, ConvertHyperbolicStateToConic)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    IO::Astrodynamics::API::DTO::StateVectorDTO stateVector;
    stateVector.epoch = 2451545.0; // J2000 epoch
    stateVector.position = {6800000.0, 0.0, 0.0};
    stateVector.velocity = {0.0, 12000.0, 0.0};
    stateVector.centerOfMotionId=earth->GetId();
    stateVector.SetFrame("J2000");
    // double mu = 398600.4418; // Earth's gravitational parameter

    auto result = ConvertStateVectorToConicOrbitalElementProxy(stateVector, earth->GetMu());
    result.centerOfMotionId = 399;
    result.SetFrame("J2000");


    auto stateVector2 = ConvertConicElementsToStateVectorAtEpochProxy(result,stateVector.epoch,earth->GetMu());
    EXPECT_NEAR(stateVector.position.x, stateVector2.position.x, 1e-6);
    EXPECT_NEAR(stateVector.position.y, stateVector2.position.y, 1e-6);
    EXPECT_NEAR(stateVector.position.z, stateVector2.position.z, 1e-6);
    EXPECT_NEAR(stateVector.velocity.x, stateVector2.velocity.x, 1e-6);
    EXPECT_NEAR(stateVector.velocity.y, stateVector2.velocity.y, 1e-6);
    EXPECT_NEAR(stateVector.velocity.z, stateVector2.velocity.z, 1e-6);
    EXPECT_NEAR(stateVector.epoch, stateVector2.epoch, 1e-6);
}
