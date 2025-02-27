/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <CelestialBody.h>
#include <OrbitalParameters.h>
#include <TDB.h>
#include <limits>
#include <StateVector.h>
#include <memory>
#include <InertialFrames.h>
#include <Aberrations.h>
#include <Barycenter.h>
#include "Constraints/RelationalOperator.h"

using namespace std::chrono_literals;

TEST(CelestialBody, SphereOfInfluence)
{
    double res = IO::Astrodynamics::Body::SphereOfInfluence(150000000000, 1.32712440018E+20, 3.98600435436E+14);
    ASSERT_DOUBLE_EQ(927132302.95950806, res);

    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    // IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);

    ASSERT_DOUBLE_EQ(925064672.53459013, earth->GetSphereOfInfluence());
    ASSERT_DOUBLE_EQ(std::numeric_limits<double>::infinity(), sun->GetSphereOfInfluence());
}

TEST(CelestialBody, HillSphere)
{
    double res = IO::Astrodynamics::Body::HillSphere(150000000000, 0, 1.32712440018E+20, 3.98600435436E+14);
    ASSERT_DOUBLE_EQ(1500581377.2140491, res);

    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    // IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);

    ASSERT_DOUBLE_EQ(1471599696.8168514, earth->GetHillSphere());
    ASSERT_DOUBLE_EQ(std::numeric_limits<double>::infinity(), sun->GetHillSphere());
}

TEST(CelestialBody, GetStateVector)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);

    double expectedData[6]{
            -2.6795375379297768E+10, 1.3270111352322429E+11, 5.7525334752378304E+10, -29765.580095900841,
            -5075.3399173890839, -2200.9299676732885
    };
    auto sv = earth->ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                                   IO::Astrodynamics::AberrationsEnum::None, epoch, *sun);
    ASSERT_EQ(
            IO::Astrodynamics::OrbitalParameters::StateVector(sun, expectedData, epoch, IO::Astrodynamics::Frames::
            InertialFrames::ICRF()), sv);

    //second overload
    auto sv2 = earth->ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                                    IO::Astrodynamics::AberrationsEnum::None, epoch);
    ASSERT_EQ(
            IO::Astrodynamics::OrbitalParameters::StateVector(sun, expectedData, epoch, IO::Astrodynamics::Frames::
            InertialFrames::ICRF()), sv2);
}

TEST(CelestialBody, GetRelativeStateVector)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto marsBarycenter = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(4, sun);

    double expectedData[6]{
            1.1967701118722568E+11, 5.5305597076056137E+10, 2.6202720828289268E+10, 8.5989974247898281E+03,
            1.5803131615538015E+04, 7.6926453157571395E+03
    };
    auto sv = earth->GetRelativeStatevector(
            marsBarycenter->ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(),
                                          IO::Astrodynamics::AberrationsEnum::None, epoch, *sun));
    ASSERT_EQ(
            IO::Astrodynamics::OrbitalParameters::StateVector(earth, expectedData, epoch, IO::Astrodynamics::Frames::
            InertialFrames::ICRF()), sv);
}

TEST(CelestialBody, IsInSphereOfInfluence)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto marsBarycenter = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(4, sun);

    ASSERT_FALSE(
            earth->IsInSphereOfInfluence(marsBarycenter->ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO
            ::Astrodynamics::AberrationsEnum::None, epoch, *sun)));

    auto fictiveBody = IO::Astrodynamics::OrbitalParameters::StateVector(
            earth, IO::Astrodynamics::Math::Vector3D(900000000.0, 0.0, 0.0),
            IO::Astrodynamics::Math::Vector3D(0.0, 1000.0, 0.0), epoch,
            IO::Astrodynamics::Frames::InertialFrames::ICRF());
    ASSERT_TRUE(earth->IsInSphereOfInfluence(fictiveBody));
}

TEST(CelestialBody, IsInHillSphere)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto marsBarycenter = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(4, sun);

    ASSERT_FALSE(
            earth->IsInHillSphere(marsBarycenter->ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::
            Astrodynamics::AberrationsEnum::None, epoch, *sun)));

    auto fictiveBody = IO::Astrodynamics::OrbitalParameters::StateVector(
            earth, IO::Astrodynamics::Math::Vector3D(1400000000.0, 0.0, 0.0),
            IO::Astrodynamics::Math::Vector3D(0.0, 1000.0, 0.0), epoch,
            IO::Astrodynamics::Frames::InertialFrames::ICRF());
    ASSERT_TRUE(earth->IsInHillSphere(fictiveBody));
}

TEST(CelestialBody, GetRadii)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    ASSERT_EQ(IO::Astrodynamics::Math::Vector3D(6378136.6, 6378136.6, 6356751.9), earth->GetRadius());
}

TEST(CelestialBody, GetFlattening)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    ASSERT_DOUBLE_EQ(0.0033528131084554157, earth->GetFlattening());
}

TEST(CelestialBody, GetAngularVelocity)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    ASSERT_NEAR(7.2921151939699377e-05, earth->GetAngularVelocity(epoch), 1E-09);
}

TEST(CelestialBody, GetSideralRotationPeriod)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    ASSERT_NEAR(23.93447176256339, earth->GetSideralRotationPeriod(epoch).GetHours().count(), 1E-08);
}

TEST(CelestialBody, FindDistanceConstraint)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);

    auto searchWindow = IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(
            IO::Astrodynamics::Time::TDB("2007 JAN 1"), IO::Astrodynamics::Time::TDB("2007 APR 1"));
    auto results = earth->FindWindowsOnDistanceConstraint(searchWindow, *moon, *earth,
                                                          IO::Astrodynamics::Constraints::RelationalOperator::GreaterThan(),
                                                          IO::Astrodynamics::AberrationsEnum::None, 400000000.0,
                                                          IO::Astrodynamics::Time::TimeSpan(86400s));

    ASSERT_EQ(4, results.size());
    ASSERT_STREQ("2007-01-08 00:11:07.628591 (TDB)", results[0].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-01-13 06:37:47.948144 (TDB)", results[0].GetEndDate().ToString().c_str());
    ASSERT_STREQ("2007-03-29 22:53:58.151896 (TDB)", results[3].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-04-01 00:01:05.185654 (TDB)", results[3].GetEndDate().ToString().c_str());
}

TEST(CelestialBody, FindOccultationConstraint)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);

    auto searchWindow = IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(
            IO::Astrodynamics::Time::TDB("2001 DEC 13"), IO::Astrodynamics::Time::TDB("2001 DEC 15"));
    auto results = earth->FindWindowsOnOccultationConstraint(searchWindow, *sun, *moon,
                                                             IO::Astrodynamics::OccultationType::Any(),
                                                             IO::Astrodynamics::AberrationsEnum::LT,
                                                             IO::Astrodynamics::Time::TimeSpan(240s));

    ASSERT_EQ(1, results.size());
    ASSERT_STREQ("2001-12-14 20:10:15.410588 (TDB)", results[0].GetStartDate().ToString().c_str());
    ASSERT_STREQ("2001-12-14 21:35:49.100520 (TDB)", results[0].GetEndDate().ToString().c_str());
}

TEST(CelestialBody, FindCenterOfMotion)
{
    ASSERT_EQ(0, IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(0));
    ASSERT_EQ(10, IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(10));
    ASSERT_EQ(0, IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(3));
    ASSERT_EQ(10, IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(399));
    ASSERT_EQ(399, IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(301));
    ASSERT_EQ(399, IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(391));
    ASSERT_EQ(399, IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(394));
}

TEST(CelestialBody, FindBarycenterOfMotion)
{
    ASSERT_EQ(0, IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(0));
    ASSERT_EQ(0, IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(10));
    ASSERT_EQ(0, IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(3));
    ASSERT_EQ(3, IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(399));
    ASSERT_EQ(3, IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(301));
    ASSERT_EQ(3, IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(391));
    ASSERT_EQ(3, IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(394));
}

TEST(CelestialBody, GetJValue)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto j2 = earth->GetJ2();
    ASSERT_NEAR(0.00108262998905, j2, 9);

    auto j3 = earth->GetJ3();
    ASSERT_NEAR(-0.00000253881, j3, 9);

    auto j4 = earth->GetJ4();
    ASSERT_NEAR(-0.00000165597, j4, 9);

    //Moon's geophysical properties doesn't exist by default
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301);
    auto moonj2 = moon->GetJ2();
    ASSERT_TRUE(std::isnan(moonj2));
}


TEST(CelestialBody, TrueSolarDayAtEpoch)
{
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    auto res1 = earth->GetTrueSolarDay(epoch);
    ASSERT_NEAR(86407.306035452566, res1.GetSeconds().count(), 1E-05);

    IO::Astrodynamics::Time::TDB epoch2("2021-MAR-26 00:00:00.0000 TDB");
    auto res2 = earth->GetTrueSolarDay(epoch2);
    ASSERT_NEAR(86400.359514701879, res2.GetSeconds().count(), 1E-05);

    IO::Astrodynamics::Time::TDB epoch3("2021-JUL-25 00:00:00.0000 TDB");
    auto res3 = earth->GetTrueSolarDay(epoch3);
    ASSERT_NEAR(86392.011764653842, res3.GetSeconds().count(), 1E-05);

    IO::Astrodynamics::Time::TDB epoch4("2021-DEC-22 00:00:00.0000 TDB");
    auto res4 = earth->GetTrueSolarDay(epoch4);
    ASSERT_NEAR(86407.114275442393, res4.GetSeconds().count(), 1E-05);
}

TEST(CelestialBody, GeosynchronousOrbitFromLongitude)
{
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto svICRF = earth->ComputeGeosynchronousOrbit(0.0, epoch).ToStateVector();
    ASSERT_NEAR(42164171.957522824, svICRF.GetPosition().Magnitude(), 1E-09);
    ASSERT_NEAR(3074.659989893702, svICRF.GetVelocity().Magnitude(), 1E-09);

    auto svECEF = svICRF.ToFrame(earth->GetBodyFixedFrame());
    ASSERT_NEAR(42164171.957522824, svECEF.GetPosition().Magnitude(), 1E-09);
    ASSERT_NEAR(0.0, svECEF.GetVelocity().Magnitude(), 1E-06);
    ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::ICRF(), svICRF.GetFrame());
}

TEST(CelestialBody, GeosynchronousOrbitFromLongitudeAndLatitude)
{
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto conics = earth->ComputeGeosynchronousOrbit(0.0, 0.0, epoch);
    auto svICRF = conics.ToStateVector();
    ASSERT_NEAR(42164171.957522817, svICRF.GetPosition().Magnitude(), 1E-09);
    ASSERT_NEAR(3074.6599898937015, svICRF.GetVelocity().Magnitude(), 1E-09);
    ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::ICRF(), svICRF.GetFrame());
    ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::ICRF(), conics.GetFrame());
}

TEST(CelestialBody, GeosynchronousOrbitFromLongitudeAndLatitude2)
{
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto conics = earth->ComputeGeosynchronousOrbit(1.0, 1.0, epoch);
    auto svICRF = conics.ToStateVector();
    ASSERT_NEAR(42164171.957522824, svICRF.GetPosition().Magnitude(), 1E-09);
    ASSERT_NEAR(3074.6599898937015, svICRF.GetVelocity().Magnitude(), 1E-09);
    ASSERT_NEAR(42164171.957522802, conics.GetSemiMajorAxis(), 1E-09);
    ASSERT_NEAR(1.0, conics.GetInclination(), 1E-02);
    ASSERT_NEAR(0.0, conics.GetEccentricity(), 1E-09);
    ASSERT_NEAR(1.1804318466570587, conics.GetRightAscendingNodeLongitude(), 1E-09);
    ASSERT_NEAR(1.5698873913048708, conics.GetPeriapsisArgument(), 1E-09);
    ASSERT_NEAR(0.0, conics.GetMeanAnomaly(), 1E-09);
    ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::ICRF(), conics.GetFrame());
    ASSERT_NEAR(42164171.957522824, svICRF.GetPosition().Magnitude(), 1E-09);
    ASSERT_NEAR(3074.6599898937015, svICRF.GetVelocity().Magnitude(), 1E-09);
    ASSERT_NEAR(-20992029.304842189, svICRF.GetPosition().GetX(), 1E-03);
    ASSERT_NEAR(8679264.3222530782, svICRF.GetPosition().GetY(), 1E-03);
    ASSERT_NEAR(35522140.608061768, svICRF.GetPosition().GetZ(), 1E-03);
    ASSERT_EQ(IO::Astrodynamics::Math::Vector3D(-1171.3783814243964, -2842.7805398311166, 2.3544303355950098),
              svICRF.GetVelocity());
    ASSERT_EQ(IO::Astrodynamics::Frames::InertialFrames::ICRF(), svICRF.GetFrame());
}
