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

    IO::Astrodynamics::Math::Matrix mtxGmst = IO::Astrodynamics::Frames::Frames::FromTEMEToITRF(utc);
    IO::Astrodynamics::Math::Quaternion qGast(mtxGmst);


    IO::Astrodynamics::OrbitalParameters::StateVector satSvITRF(
            earth, satSvTEME.GetPosition().Rotate(qGast),
            satSvTEME.GetVelocity().Rotate(qGast.Conjugate()), utc.ToTDB(),
            earth->GetBodyFixedFrame());

    IO::Astrodynamics::Frames::Frames temeFrame("TEME");
    auto satSvITRF2 = satSvTEME.ToFrame(temeFrame, mtxGmst);//GOOD

    const double OMEGA_EARTH = 7.2921150e-5;
    IO::Astrodynamics::Math::Vector3D omega_itrf(0.0, 0.0, OMEGA_EARTH);
    auto cross = omega_itrf.CrossProduct(satSvITRF.GetPosition());
    auto vitrf = satSvTEME.GetVelocity().Rotate(qGast);

    auto resVel = vitrf - cross;//GOOD

    auto diffec = satSvITRF.ToFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF()) - siteSv.ToFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF());
    auto eq = diffec.ToEquatorialCoordinates();

    auto raSkyField = 331.59;
    double decSkyField = 11.859;

    double raObs = 331.5980;
    double decObs = 11.8474;

    double ra = eq.GetRA() * IO::Astrodynamics::Constants::RAD_DEG;
    double dec = eq.GetDec() * IO::Astrodynamics::Constants::RAD_DEG;

    double deltaRAObs = abs(ra - raObs);
    double deltaDecObs = abs(dec - decObs);

    double deltaRASkyFieldObs = abs(raSkyField - raObs);
    double deltaDecSkyFieldObs = abs(decSkyField - decObs);
    ASSERT_LT(deltaRAObs, deltaRASkyFieldObs);
    ASSERT_LT(deltaDecObs, deltaDecSkyFieldObs);
}


