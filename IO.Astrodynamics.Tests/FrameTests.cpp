#include <cmath>
#include <Constants.h>
#include<gtest/gtest.h>
#include<Frames.h>
#include <InertialFrames.h>
#include <Site.h>
#include<TDB.h>
#include <TestParameters.h>
#include <TLE.h>
#include <Type.h>

TEST(Frames, ToTEME)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::string lines[3]{
        "CZ-3C DEB", "1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995",
        "2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468"
    };

    IO::Astrodynamics::Time::UTC utc("2024-8-26T22:34:20.00000Z");


    IO::Astrodynamics::OrbitalParameters::TLE tle(earth, lines);
    auto satSvTEME = tle.ToStateVector(utc.ToTDB());

    IO::Astrodynamics::Coordinates::Planetodetic planetodetic(19.89367 * IO::Astrodynamics::Constants::DEG_RAD,
                                                              47.91748 * IO::Astrodynamics::Constants::DEG_RAD, 984);

    IO::Astrodynamics::Sites::Site site(399123, "k88", planetodetic, earth, std::string(SitePath));
    auto siteSv = site.GetStateVector(earth->GetBodyFixedFrame(), utc.ToTDB());

    IO::Astrodynamics::Math::Matrix mtxGmst = IO::Astrodynamics::Frames::Frames::ToITRF(utc.ToTDB());
    // IO::Astrodynamics::Math::Matrix mtxPOM = IO::Astrodynamics::Frames::Frames::PolarMotion(utc.ToTDB());
    IO::Astrodynamics::Math::Quaternion qGast(mtxGmst);
    // IO::Astrodynamics::Math::Quaternion qPOM(mtxPOM);


    IO::Astrodynamics::OrbitalParameters::StateVector satSvITRF(
        earth, satSvTEME.GetPosition().Rotate(qGast),
        satSvTEME.GetVelocity(), utc.ToTDB(),
        earth->GetBodyFixedFrame());


    auto diffec = satSvITRF.ToFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF()) - siteSv.ToFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF());
    auto eq = diffec.ToEquatorialCoordinates();
    double ra = eq.GetRA() * IO::Astrodynamics::Constants::RAD_DEG;
    double dec = eq.GetDec() * IO::Astrodynamics::Constants::RAD_DEG;
}


TEST(TDB, ExtractTime)
{
    int year, month, day, hour, minute;
    double second;
    IO::Astrodynamics::Frames::Frames::ExtractDateTimeComponents("2021-02-03 13:14:15.60 TDB", year, month, day,
                                                                 hour, minute, second);

    ASSERT_EQ(2021, year);
    ASSERT_EQ(2, month);
    ASSERT_EQ(3, day);
    ASSERT_EQ(13, hour);
    ASSERT_EQ(14, minute);
    ASSERT_DOUBLE_EQ(15.60, second);
}
