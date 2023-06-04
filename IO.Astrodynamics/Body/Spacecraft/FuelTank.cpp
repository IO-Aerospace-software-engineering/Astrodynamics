/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <FuelTank.h>
#include <InvalidArgumentException.h>

IO::Astrodynamics::Body::Spacecraft::FuelTank::FuelTank(const std::string &serialNumber, const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft, const double capacity, const double quantity) : m_spacecraft{spacecraft}
{
    if (serialNumber.empty())
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Serial number must be filled");
    }

    if (quantity <= 0)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Quantity must be a positive number");
    }

    if (capacity <= 0)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Capacity must be a positive number");
    }

    if (quantity > capacity)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Quantity must be lower or equal to capacity");
    }

    const_cast<double &>(m_initialQuantity) = m_quantity = quantity;
    const_cast<double &>(m_capacity) = capacity;
    const_cast<std::string &>(m_serialNumber) = serialNumber;
}

const IO::Astrodynamics::Body::Spacecraft::Spacecraft &IO::Astrodynamics::Body::Spacecraft::FuelTank::GetSpacecraft() const
{
    return m_spacecraft;
}

std::string IO::Astrodynamics::Body::Spacecraft::FuelTank::GetSerialNumber() const
{
    return m_serialNumber;
}

double IO::Astrodynamics::Body::Spacecraft::FuelTank::GetQuantity() const
{
    return m_quantity;
}

double IO::Astrodynamics::Body::Spacecraft::FuelTank::GetCapacity() const
{
    return m_capacity;
}

double IO::Astrodynamics::Body::Spacecraft::FuelTank::GetInitialQuantity() const
{
    return m_initialQuantity;
}

void IO::Astrodynamics::Body::Spacecraft::FuelTank::UpdateFuelQuantity(const double quantityToAdd)
{
    if (m_quantity + quantityToAdd < 0)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Not enough fuel");
    }

    if (m_quantity + quantityToAdd > m_capacity)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Insufficient capacity ");
    }

    m_quantity += quantityToAdd;
}

bool IO::Astrodynamics::Body::Spacecraft::FuelTank::IsEmpty() const
{
    return m_quantity <= 0.0;
}
