#include <gtest/gtest.h>
#include <Spacecraft.h>
#include <InvalidArgumentException.h>
#include <InertialFrames.h>
#include <Vectors.h>
#include <TestParameters.h>

using namespace std::chrono_literals;



TEST(Spacecraft, Initialization) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-1, "Spacecraft1", 1000.0, 3000.0,  std::string(SpacecraftPath), std::move(orbitalParams));
    ASSERT_EQ(-1, spc.GetId());
    ASSERT_STREQ("SPACECRAFT1", spc.GetName().c_str());
    ASSERT_STREQ("Data/User/Spacecrafts/Spacecraft1", spc.GetFilesPath().c_str());
    ASSERT_DOUBLE_EQ(1000.0, spc.GetMass());
    ASSERT_DOUBLE_EQ(0.000000066743, spc.GetMu());
}

TEST(Spacecraft, InvalidId) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    ASSERT_THROW(IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(1, "Spacecraft1", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)), IO::Astrodynamics::Exception::SDKException);
}

TEST(Spacecraft, AddPayload) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddPayload("p1", "payload1", 300.0);
    ASSERT_DOUBLE_EQ(2200.0, s.GetMass());
}

TEST(Spacecraft, ReleasePayload) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddPayload("p1", "payload1", 300.0);
    s.ReleasePayload("p1");
    ASSERT_DOUBLE_EQ(1900.0, s.GetMass());
}

TEST(Spacecraft, ReleaseInvalidPayload) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    s.AddPayload("p1", "payload1", 300.0);
    ASSERT_THROW(s.ReleasePayload("p13"), IO::Astrodynamics::Exception::InvalidArgumentException);
    ASSERT_THROW(s.ReleasePayload(""), IO::Astrodynamics::Exception::InvalidArgumentException);
    ASSERT_DOUBLE_EQ(2200.0, s.GetMass());
}

TEST(Spacecraft, EngineInvalidISP) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    ASSERT_THROW(s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, -450.0, 50.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, EngineInvalidFuelFlow) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    ASSERT_THROW(s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, -50.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, GetFuelTank) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    auto fueltank = s.GetFueltank("ft1");
    ASSERT_STREQ("ft1", fueltank->GetSerialNumber().c_str());
}

TEST(Spacecraft, EngineInvalidSerialNumber) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    ASSERT_THROW(s.AddEngine("", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankOverQuantity) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("ft1", 500.0, 600.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankEmptySerialNumber) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("", 1500.0, 600.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankInvalidCapacity) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("", -300.0, 600.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankInvalidQuantity) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("", 300.0, -600.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, FuelTankInvalidName) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("", 300.0, -600.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Spacecraft, GetEngine) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    s.AddEngine("sn1", "eng1", "ft1", {1.0, 2.0, 3.0}, {4.0, 5.0, 6.0}, 450.0, 50.0);
    auto engine = s.GetEngine("sn1");
    ASSERT_STREQ("sn1", engine->GetSerialNumber().c_str());
}

TEST(Spacecraft, Orientation) {
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams)};

    ASSERT_EQ(IO::Astrodynamics::Tests::VectorY, s.Front);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorZ, s.Top);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorX, s.Right);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorY.Reverse(), s.Back);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorZ.Reverse(), s.Bottom);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorX.Reverse(), s.Left);
}


TEST(Spacecraft, Orientation2) {

    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(1.0, 2.0, 3.0),
                                                                                                                                             IO::Astrodynamics::Math::Vector3D(4.0, 5.0, 6.0),
                                                                                                                                             IO::Astrodynamics::Time::TDB(100.0s),
                                                                                                                                             IO::Astrodynamics::Frames::InertialFrames::GetICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, std::string(SpacecraftPath), std::move(orbitalParams), IO::Astrodynamics::Tests::VectorX, IO::Astrodynamics::Tests::VectorY};

    ASSERT_EQ(IO::Astrodynamics::Tests::VectorX, s.Front);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorY, s.Top);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorZ, s.Right);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorX.Reverse(), s.Back);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorY.Reverse(), s.Bottom);
    ASSERT_EQ(IO::Astrodynamics::Tests::VectorZ.Reverse(), s.Left);
}
