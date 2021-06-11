#include<IlluminationAngle.h>
IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::Phase(std::string("PHASE"));
IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::Incidence(std::string("INCIDENCE"));
IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::Emission(std::string("EMISSION"));

IO::SDK::IlluminationAngle::IlluminationAngle(const std::string& name):m_name{name}
{

}

const char *IO::SDK::IlluminationAngle::ToCharArray() const
{
    return m_name.c_str();
}