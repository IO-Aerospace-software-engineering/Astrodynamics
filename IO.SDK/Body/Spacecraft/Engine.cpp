/**
 * @file Engine.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Engine.h>
#include <InvalidArgumentException.h>
#include <Constants.h>
#include <cmath>
#include <FuelTank.h>
#include <Spacecraft.h>

IO::SDK::Body::Spacecraft::Engine::Engine(const std::string &serialNumber, const std::string &name, const IO::SDK::Body::Spacecraft::FuelTank &fueltank, const Math::Vector3D &position, const Math::Vector3D &orientation, const double isp, const double fuelFlow)
    : m_fuelTank{fueltank}, m_position{position}, m_orientation{orientation}
{
    if (serialNumber.empty())
    {
        throw IO::SDK::Exception::InvalidArgumentException("Serial number must be filled");
    }

    if (name.empty())
    {
        throw IO::SDK::Exception::InvalidArgumentException("Name number must be filled");
    }

    if (isp <= 0.0)
    {
        throw IO::SDK::Exception::InvalidArgumentException("ISP must be greater than 0.0");
    }

    if (fuelFlow <= 0.0)
    {
        throw IO::SDK::Exception::InvalidArgumentException("Fuel flow must be greater than 0.0");
    }
    const_cast<double &>(m_isp) = isp;
    const_cast<double &>(m_fuelFlow) = fuelFlow;
    const_cast<double &>(m_thrust) = isp * fuelFlow * IO::SDK::Constants::g0;
    const_cast<std::string &>(m_serialNumber) = serialNumber;
    const_cast<std::string &>(m_name) = name;
}

double IO::SDK::Body::Spacecraft::Engine::GetFuelFlow() const
{
    return m_fuelFlow;
}

double IO::SDK::Body::Spacecraft::Engine::GetISP() const
{
    return m_isp;
}

std::string IO::SDK::Body::Spacecraft::Engine::GetName() const
{
    return m_name;
}

const IO::SDK::Math::Vector3D &IO::SDK::Body::Spacecraft::Engine::GetOrientation() const
{
    return m_orientation;
}

const IO::SDK::Math::Vector3D &IO::SDK::Body::Spacecraft::Engine::GetPosition() const
{
    return m_position;
}

std::string IO::SDK::Body::Spacecraft::Engine::GetSerialNumber() const
{
    return m_serialNumber;
}

double IO::SDK::Body::Spacecraft::Engine::GetRemainingDeltaV() const
{
    double totalMass = m_fuelTank.GetSpacecraft().GetMass();
    return IO::SDK::Body::Spacecraft::Engine::ComputeDeltaV(m_isp, totalMass, (totalMass - m_fuelTank.GetQuantity()));
}

const IO::SDK::Body::Spacecraft::FuelTank &IO::SDK::Body::Spacecraft::Engine::GetFuelTank() const
{
    return m_fuelTank;
}

double IO::SDK::Body::Spacecraft::Engine::GetThrust() const
{
    return m_thrust;
}

bool IO::SDK::Body::Spacecraft::Engine::operator==(const IO::SDK::Body::Spacecraft::Engine &other) const
{
    return m_serialNumber == other.m_serialNumber;
}

bool IO::SDK::Body::Spacecraft::Engine::operator!=(const IO::SDK::Body::Spacecraft::Engine &other) const
{
    return !(m_serialNumber == other.m_serialNumber);
}

double IO::SDK::Body::Spacecraft::Engine::ComputeDeltaV(double isp, double initialMass, double finalMass)
{
    return isp * IO::SDK::Constants::g0 * std::log(initialMass / finalMass);
}

IO::SDK::Time::TimeSpan IO::SDK::Body::Spacecraft::Engine::ComputeDeltaT(double isp, double initialMass, double fuelFlow, double deltaV)
{
    return IO::SDK::Time::TimeSpan(std::chrono::duration<double>(initialMass / fuelFlow * (1 - std::exp(-deltaV / (isp * IO::SDK::Constants::g0)))));
}

double IO::SDK::Body::Spacecraft::Engine::ComputeDeltaM(double isp, double initialMass, double deltaV)
{
    return initialMass * (1 - std::exp(-deltaV / (isp * IO::SDK::Constants::g0)));
}

double IO::SDK::Body::Spacecraft::Engine::Burn(const IO::SDK::Time::TimeSpan &duration)
{
    double fuelRequired = m_fuelFlow * duration.GetSeconds().count();
    if (m_fuelTank.GetQuantity() < fuelRequired)
    {
        throw IO::SDK::Exception::InvalidArgumentException("Not enought fuel to satisfy burn duration");
    }

    const_cast<IO::SDK::Body::Spacecraft::FuelTank &>(m_fuelTank).UpdateFuelQuantity(-fuelRequired);

    return fuelRequired;
}
