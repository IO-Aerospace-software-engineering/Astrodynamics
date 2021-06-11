#include <gtest/gtest.h>
#include <Spacecraft.h>
#include <Constants.h>
#include <SDKException.h>
#include <InvalidArgumentException.h>
#include <CelestialBody.h>
#include <memory>
#include <InertialFrames.h>
#include <Engine.h>

using namespace std::chrono_literals;

TEST(Spacecraft, Initialization)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft spc(-1, "Spacecraft1", 1000.0, 3000.0, "Mission1", std::move(orbitalParams));
	ASSERT_EQ(-1, spc.GetId());
	ASSERT_STREQ("Spacecraft1", spc.GetName().c_str());
	ASSERT_STREQ("Mission1", spc.GetMissionPrefix().c_str());
	ASSERT_STREQ("Data/Spacecraft1_Mission1", spc.GetFilesPath().c_str());
	ASSERT_DOUBLE_EQ(1000.0, spc.GetMass());
	ASSERT_DOUBLE_EQ(0.000000066743, spc.GetMu());
}

TEST(Spacecraft, InvalidId)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	ASSERT_THROW(IO::SDK::Body::Spacecraft::Spacecraft spc(1, "Spacecraft1", 1000.0, 3000.0, "Mission1", std::move(orbitalParams)), IO::SDK::Exception::SDKException);
}

TEST(Spacecraft, AddPayload)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
	s.AddPayload("p1", "payload1", 300.0);
	ASSERT_DOUBLE_EQ(2200.0, s.GetMass());
}

TEST(Spacecraft, ReleasePayload)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
	s.AddPayload("p1", "payload1", 300.0);
	s.ReleasePayload("p1");
	ASSERT_DOUBLE_EQ(1900.0, s.GetMass());
}

TEST(Spacecraft, ReleaseInvalidPayload)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
	s.AddPayload("p1", "payload1", 300.0);
	ASSERT_THROW(s.ReleasePayload("p13"), IO::SDK::Exception::InvalidArgumentException);
	ASSERT_THROW(s.ReleasePayload(""), IO::SDK::Exception::InvalidArgumentException);
	ASSERT_DOUBLE_EQ(2200.0, s.GetMass());
}

TEST(Spacecraft, EngineInvalidISP)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	ASSERT_THROW(s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, -450.0, 50.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, EngineInvalidFuelFlow)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	ASSERT_THROW(s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, -50.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, GetFuelTank)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	auto fueltank = s.GetFueltank("ft1");
	ASSERT_STREQ("ft1", fueltank->GetSerialNumber().c_str());
}

TEST(Spacecraft, EngineInvalidSerialNumber)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	ASSERT_THROW(s.AddEngine("", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankOverQuantity)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	ASSERT_THROW(s.AddFuelTank("ft1", 500.0, 600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankEmptySerialNumber)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	ASSERT_THROW(s.AddFuelTank("", 1500.0, 600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankInvalidCapacity)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	ASSERT_THROW(s.AddFuelTank("", -300.0, 600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankInvalidQuantity)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	ASSERT_THROW(s.AddFuelTank("", 300.0, -600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankInvalidName)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	ASSERT_THROW(s.AddFuelTank("", 300.0, -600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(Spacecraft, GetEngine)
{
	const auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
	std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::ICRF);
	IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
	s.AddFuelTank("ft1", 1000.0, 900.0);
	s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
	auto engine = s.GetEngine("sn1");
	ASSERT_STREQ("sn1", engine->GetSerialNumber().c_str());
}

TEST(Spacecraft, Orientation)
{
	
}
