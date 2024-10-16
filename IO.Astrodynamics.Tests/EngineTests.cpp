#include <gtest/gtest.h>
#include <Engine.h>
#include <FuelTank.h>
#include <Spacecraft.h>
#include <InvalidArgumentException.h>
#include <StateVector.h>
#include <CelestialBody.h>
#include <chrono>
#include <TimeSpan.h>
#include <memory>
#include<InertialFrames.h>
#include "TestParameters.h"

using namespace std::chrono_literals;

TEST(Engine, Initialization)
{
	auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
	std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0), IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0), IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                                                 IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::OrbitalParameters::StateOrientation attitude(IO::Astrodynamics::Time::TDB(100.0s), IO::Astrodynamics::Frames::InertialFrames::ICRF());
	IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
	auto eng = s.GetEngine("sn1");
	ASSERT_STREQ("eng1", eng->GetName().c_str());
	ASSERT_STREQ("sn1", eng->GetSerialNumber().c_str());
	ASSERT_DOUBLE_EQ(450.0, eng->GetISP());
	ASSERT_DOUBLE_EQ(50.0, eng->GetFuelFlow());
	ASSERT_DOUBLE_EQ(220649.625, eng->GetThrust());
	ASSERT_DOUBLE_EQ(2832.4963857746311, eng->GetRemainingDeltaV());
	ASSERT_DOUBLE_EQ(1.0, eng->GetPosition().GetX());
	ASSERT_DOUBLE_EQ(2.0, eng->GetPosition().GetY());
	ASSERT_DOUBLE_EQ(3.0, eng->GetPosition().GetZ());
	ASSERT_DOUBLE_EQ(4.0, eng->GetOrientation().GetX());
	ASSERT_DOUBLE_EQ(5.0, eng->GetOrientation().GetY());
	ASSERT_DOUBLE_EQ(6.0, eng->GetOrientation().GetZ());
}

TEST(Engine, DeltaV)
{
	double deltaV = IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaV(300.0, 3000.0, 2000.0);

	ASSERT_DOUBLE_EQ(1192.876320728679, deltaV);
}

TEST(Engine, DeltaT)
{
	IO::Astrodynamics::Time::TimeSpan deltaT = IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaT(300.0, 3000.0, 100, 1192.876320728679);
	ASSERT_DOUBLE_EQ(IO::Astrodynamics::Time::TimeSpan(10.0s).GetSeconds().count(), deltaT.GetSeconds().count());
}

TEST(Engine, DeltaM)
{
	double deltaM = IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaM(300.0, 3000.0, 1192.876320728679);
	ASSERT_DOUBLE_EQ(1000.0, deltaM);
}
