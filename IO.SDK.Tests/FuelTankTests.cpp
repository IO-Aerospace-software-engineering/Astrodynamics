#include <gtest/gtest.h>
#include <FuelTank.h>
#include <Spacecraft.h>
#include <InvalidArgumentException.h>
#include <CelestialBody.h>

using namespace std::chrono_literals;
TEST(FuelTank, Initialization)
{
    auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    const IO::SDK::Body::Spacecraft::FuelTank *ft = s.GetFueltank("ft1");
    ASSERT_DOUBLE_EQ(1000.0, ft->GetCapacity());
    ASSERT_DOUBLE_EQ(900.0, ft->GetQuantity());
    ASSERT_DOUBLE_EQ(900.0, ft->GetInitialQuantity());
    ASSERT_STREQ("ft1", ft->GetSerialNumber().c_str());
    ASSERT_EQ(&s, &ft->GetSpacecraft());
}

TEST(FuelTank, OverQuantity)
{
    auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("ft1", 500.0, 600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(FuelTank, EmptySerialNumber)
{
    auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01",std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("", 1500.0, 600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(FuelTank, InvalidCapacity)
{
    auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("", -300.0, 600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(FuelTank, InvalidQuantity)
{
    auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    ASSERT_THROW(s.AddFuelTank("", 300.0, -600.0), IO::SDK::Exception::InvalidArgumentException);
}

TEST(FuelTank, UpdateFuelQuantity)
{
    auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    IO::SDK::Body::Spacecraft::FuelTank *ft = s.GetFueltank("ft1");

    ft->UpdateFuelQuantity(-200.0);
    ASSERT_DOUBLE_EQ(900.0,ft->GetInitialQuantity());
    ASSERT_DOUBLE_EQ(700.0,ft->GetQuantity());
    ASSERT_DOUBLE_EQ(1000.0,ft->GetCapacity());

    ft->UpdateFuelQuantity(100.0);
    ASSERT_DOUBLE_EQ(800.0,ft->GetQuantity());

    ASSERT_THROW( ft->UpdateFuelQuantity(300.0), IO::SDK::Exception::InvalidArgumentException);
    ASSERT_THROW( ft->UpdateFuelQuantity(-2000.0), IO::SDK::Exception::InvalidArgumentException);

    ASSERT_DOUBLE_EQ(800.0,ft->GetQuantity());
}

TEST(FuelTank, IsEmpty)
{
    auto earth=std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth");
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(earth, IO::SDK::Math::Vector3D(1.0, 2.0, 3.0), IO::SDK::Math::Vector3D(4.0, 5.0, 6.0), IO::SDK::Time::TDB(100.0s), IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::OrbitalParameters::StateOrientation attitude(IO::SDK::Time::TDB(100.0s),IO::SDK::Frames::InertialFrames::GetICRF());
    IO::SDK::Body::Spacecraft::Spacecraft s{-1, "sptest", 1000.0, 3000.0, "ms01", std::move(orbitalParams)};
    s.AddFuelTank("ft1", 1000.0, 900.0);
    IO::SDK::Body::Spacecraft::FuelTank *ft = s.GetFueltank("ft1");

    ft->UpdateFuelQuantity(-900.0);
    ASSERT_TRUE(ft->IsEmpty());
}