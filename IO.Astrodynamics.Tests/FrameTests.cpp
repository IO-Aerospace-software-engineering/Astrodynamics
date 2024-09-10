#include <cmath>
#include <Constants.h>
#include<gtest/gtest.h>
#include<Frames.h>
#include <InertialFrames.h>
#include <Site.h>
#include <TestParameters.h>
#include <TLE.h>

TEST(Frames, FromITRFToTEME)
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

    IO::Astrodynamics::Math::Matrix mtxGmst = IO::Astrodynamics::Frames::Frames::FromTEMEToITRF(utc);
    IO::Astrodynamics::Math::Quaternion qGast(mtxGmst);


    IO::Astrodynamics::OrbitalParameters::StateVector satSvITRF(
        earth, satSvTEME.GetPosition().Rotate(qGast),
        satSvTEME.GetVelocity().Rotate(qGast.Conjugate()), utc.ToTDB(),
        earth->GetBodyFixedFrame());

    IO::Astrodynamics::Frames::Frames temeFrame("TEME");
    auto satSvITRF2 = satSvTEME.ToFrame(temeFrame, mtxGmst); //GOOD

    auto diffec = satSvITRF.ToFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF()) - siteSv.ToFrame(
        IO::Astrodynamics::Frames::InertialFrames::ICRF());
    auto eq = diffec.ToEquatorialCoordinates();

    auto raSkyField = 331.59;
    double decSkyField = 11.859;

    double raObs = 331.5980;
    double decObs = 11.8474;

    double ra = eq.GetRA() * IO::Astrodynamics::Constants::RAD_DEG;
    double dec = eq.GetDec() * IO::Astrodynamics::Constants::RAD_DEG;

    //Delta relative to observation
    double deltaRAObs = abs(ra - raObs);
    double deltaDecObs = abs(dec - decObs);

    //Delta relative to observation from skyfield
    double deltaRASkyFieldObs = abs(raSkyField - raObs);
    double deltaDecSkyFieldObs = abs(decSkyField - decObs);
    ASSERT_LT(deltaRAObs, deltaRASkyFieldObs);
    ASSERT_LT(deltaDecObs, deltaDecSkyFieldObs);
}

TEST(Frames, FromTEMEToITRF)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::string lines[3]{
        "CZ-3C DEB", "1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995",
        "2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468"
    };

    IO::Astrodynamics::Time::UTC utc("2024-8-26T22:34:20.00000Z");

    IO::Astrodynamics::OrbitalParameters::TLE tle(earth, lines);
    auto satSvTEME = tle.ToStateVector(utc.ToTDB());

    auto satSvITRF = satSvTEME.ToFrame(earth->GetBodyFixedFrame()); //GOOD

    auto satSvTEME2 = satSvITRF.ToFrame(tle.GetFrame());

    ASSERT_NEAR(satSvTEME.GetPosition().GetX(), satSvTEME2.GetPosition().GetX(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetPosition().GetY(), satSvTEME2.GetPosition().GetY(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetPosition().GetZ(), satSvTEME2.GetPosition().GetZ(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetVelocity().GetX(), satSvTEME2.GetVelocity().GetX(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetVelocity().GetY(), satSvTEME2.GetVelocity().GetY(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetVelocity().GetZ(), satSvTEME2.GetVelocity().GetZ(), 1e-8);
}

TEST(Frames, FromTEMEToICRF)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::string lines[3]{
        "CZ-3C DEB", "1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995",
        "2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468"
    };

    IO::Astrodynamics::Time::UTC utc("2024-8-26T22:34:20.00000Z");


    IO::Astrodynamics::OrbitalParameters::TLE tle(earth, lines);
    auto satSvTEME = tle.ToStateVector(utc.ToTDB());

    //IO::Astrodynamics::Math::Matrix mtxToITRF = IO::Astrodynamics::Frames::Frames::FromTEMEToITRF(utc);

    IO::Astrodynamics::Frames::Frames temeFrame("TEME");
    auto satSvICRF = satSvTEME.ToFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF()); //GOOD

    //IO::Astrodynamics::Math::Matrix mtxToTEME = IO::Astrodynamics::Frames::Frames::FromITRFToTEME(utc);
    auto satSvTEME2 = satSvICRF.ToFrame(tle.GetFrame());

    ASSERT_NEAR(satSvTEME.GetPosition().GetX(), satSvTEME2.GetPosition().GetX(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetPosition().GetY(), satSvTEME2.GetPosition().GetY(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetPosition().GetZ(), satSvTEME2.GetPosition().GetZ(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetVelocity().GetX(), satSvTEME2.GetVelocity().GetX(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetVelocity().GetY(), satSvTEME2.GetVelocity().GetY(), 1e-8);
    ASSERT_NEAR(satSvTEME.GetVelocity().GetZ(), satSvTEME2.GetVelocity().GetZ(), 1e-8);
}
