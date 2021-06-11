#include "Payload.h"
#include "InvalidArgumentException.h"

IO::SDK::Body::Spacecraft::Payload::Payload(const std::string &serialNumber, const std::string &name, const double mass)
{
    if (name.empty())
    {
        throw IO::SDK::Exception::InvalidArgumentException("Payload must have a name");
    }

    if (serialNumber.empty())
    {
        throw IO::SDK::Exception::InvalidArgumentException("Payload must have a serial number");
    }

    if (mass <= 0)
    {
        throw IO::SDK::Exception::InvalidArgumentException("Payload must have a positive mass");
    }
    
    const_cast<std::string &>(m_name) = name;
    const_cast<std::string &>(m_serialNumber) = serialNumber;
    const_cast<double &>(m_mass) = mass;
}

double IO::SDK::Body::Spacecraft::Payload::GetMass() const
{
    return m_mass;
}

std::string IO::SDK::Body::Spacecraft::Payload::GetName() const
{
    return m_name;
}

std::string IO::SDK::Body::Spacecraft::Payload::GetSerialNumber() const
{
    return m_serialNumber;
}
