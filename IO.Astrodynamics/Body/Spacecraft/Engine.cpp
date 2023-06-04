/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <InvalidArgumentException.h>
#include <Constants.h>
#include <Spacecraft.h>

IO::Astrodynamics::Body::Spacecraft::Engine::Engine(const std::string &serialNumber, const std::string &name, const IO::Astrodynamics::Body::Spacecraft::FuelTank &fueltank, const Math::Vector3D &position, const Math::Vector3D &orientation, const double isp, const double fuelFlow)
    : m_fuelTank{fueltank}, m_position{position}, m_orientation{orientation}
{
    if (serialNumber.empty())
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Serial number must be filled");
    }

    if (name.empty())
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Name number must be filled");
    }

    if (isp <= 0.0)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("ISP must be greater than 0.0");
    }

    if (fuelFlow <= 0.0)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Fuel flow must be greater than 0.0");
    }
    const_cast<double &>(m_isp) = isp;
    const_cast<double &>(m_fuelFlow) = fuelFlow;
    const_cast<double &>(m_thrust) = isp * fuelFlow * IO::Astrodynamics::Constants::g0;
    const_cast<std::string &>(m_serialNumber) = serialNumber;
    const_cast<std::string &>(m_name) = name;
}

double IO::Astrodynamics::Body::Spacecraft::Engine::GetFuelFlow() const
{
    return m_fuelFlow;
}

double IO::Astrodynamics::Body::Spacecraft::Engine::GetISP() const
{
    return m_isp;
}

std::string IO::Astrodynamics::Body::Spacecraft::Engine::GetName() const
{
    return m_name;
}

const IO::Astrodynamics::Math::Vector3D &IO::Astrodynamics::Body::Spacecraft::Engine::GetOrientation() const
{
    return m_orientation;
}

const IO::Astrodynamics::Math::Vector3D &IO::Astrodynamics::Body::Spacecraft::Engine::GetPosition() const
{
    return m_position;
}

std::string IO::Astrodynamics::Body::Spacecraft::Engine::GetSerialNumber() const
{
    return m_serialNumber;
}

double IO::Astrodynamics::Body::Spacecraft::Engine::GetRemainingDeltaV() const
{
    double totalMass = m_fuelTank.GetSpacecraft().GetMass();
    return IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaV(m_isp, totalMass, (totalMass - m_fuelTank.GetQuantity()));
}

const IO::Astrodynamics::Body::Spacecraft::FuelTank &IO::Astrodynamics::Body::Spacecraft::Engine::GetFuelTank() const
{
    return m_fuelTank;
}

double IO::Astrodynamics::Body::Spacecraft::Engine::GetThrust() const
{
    return m_thrust;
}

bool IO::Astrodynamics::Body::Spacecraft::Engine::operator==(const IO::Astrodynamics::Body::Spacecraft::Engine &other) const
{
    return m_serialNumber == other.m_serialNumber;
}

bool IO::Astrodynamics::Body::Spacecraft::Engine::operator!=(const IO::Astrodynamics::Body::Spacecraft::Engine &other) const
{
    return !(m_serialNumber == other.m_serialNumber);
}

double IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaV(double isp, double initialMass, double finalMass)
{
    return isp * IO::Astrodynamics::Constants::g0 * std::log(initialMass / finalMass);
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaT(double isp, double initialMass, double fuelFlow, double deltaV)
{
    return IO::Astrodynamics::Time::TimeSpan{std::chrono::duration<double>(initialMass / fuelFlow * (1 - std::exp(-deltaV / (isp * IO::Astrodynamics::Constants::g0))))};
}

double IO::Astrodynamics::Body::Spacecraft::Engine::ComputeDeltaM(double isp, double initialMass, double deltaV)
{
    return initialMass * (1 - std::exp(-deltaV / (isp * IO::Astrodynamics::Constants::g0)));
}

double IO::Astrodynamics::Body::Spacecraft::Engine::Burn(const IO::Astrodynamics::Time::TimeSpan &duration)
{
    double fuelRequired = m_fuelFlow * duration.GetSeconds().count();
    if (m_fuelTank.GetQuantity() < fuelRequired)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Not enought fuel to satisfy burn duration");
    }

    const_cast<IO::Astrodynamics::Body::Spacecraft::FuelTank &>(m_fuelTank).UpdateFuelQuantity(-fuelRequired);

    return fuelRequired;
}
