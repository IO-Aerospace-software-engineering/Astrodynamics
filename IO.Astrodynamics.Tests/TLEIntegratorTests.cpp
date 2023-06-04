#include <gtest/gtest.h>
#include <TLEIntegrator.h>
#include <Spacecraft.h>
#include "InertialFrames.h"
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(TLEIntegrator, Integrate)
{

    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399);

    std::string lines[3]{"ISS (ZARYA)", "1 25544U 98067A   21096.43776852  .00000912  00000-0  24825-4 0  9997", "2 25544  51.6463 337.6022 0002945 188.9422 344.4138 15.48860043277477"}; //2021-04-06 10:31:32.385783 TDB
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> tle = std::make_unique<IO::SDK::OrbitalParameters::TLE>(earth, lines);
    auto str = tle->GetEpoch().ToString();
    auto localTLE = dynamic_cast<IO::SDK::OrbitalParameters::TLE *>(tle.get());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft spc(-12, "spc12", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(tle));
    IO::SDK::Integrators::TLEIntegrator integrator(*localTLE, IO::SDK::Time::TimeSpan(60s));

    //Just to activate cache and evaluate optimized integration
    auto sv = spc.GetOrbitalParametersAtEpoch()->ToStateVector();

#ifdef DEBUG
    auto t1 = std::chrono::high_resolution_clock::now();
#endif
    auto stateVector = integrator.Integrate(spc, spc.GetOrbitalParametersAtEpoch()->ToStateVector()); //2021-04-06 10:32:32.385783 TDB
#ifdef DEBUG
    auto t2 = std::chrono::high_resolution_clock::now();

    std::chrono::duration<double, std::milli> micros_double = t2 - t1;

    ASSERT_LT(micros_double.count(), 0.01);
#endif
    ASSERT_DOUBLE_EQ(-6201822.8792385599, stateVector.GetPosition().GetX());
    ASSERT_DOUBLE_EQ(2769575.7618307304, stateVector.GetPosition().GetY());
    ASSERT_DOUBLE_EQ(248942.50349276056, stateVector.GetPosition().GetZ());
    ASSERT_DOUBLE_EQ(-2145.9775555620063, stateVector.GetVelocity().GetX());
    ASSERT_DOUBLE_EQ(-4250.1793473001053, stateVector.GetVelocity().GetY());
    ASSERT_DOUBLE_EQ(-6003.797568963455, stateVector.GetVelocity().GetZ());

    IO::SDK::Time::TDB epoch(670977152.38578331s);
    ASSERT_EQ(epoch, stateVector.GetEpoch());
}