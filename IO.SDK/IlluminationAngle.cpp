/**
 * @file IlluminationAngle.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<IlluminationAngle.h>

IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::_Phase(std::string("PHASE"));
IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::_Incidence(std::string("INCIDENCE"));
IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::_Emission(std::string("EMISSION"));

/**
 * @brief Construct a new IO::SDK::IlluminationAngle::IlluminationAngle object
 * 
 * @param name 
 */
IO::SDK::IlluminationAngle::IlluminationAngle(const std::string& name):m_name{name}
{

}

/**
 * @brief Get illumination angle char array
 * 
 * @return const char* 
 */
const char *IO::SDK::IlluminationAngle::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::IlluminationAngle& IO::SDK::IlluminationAngle::Phase()
{
    return _Phase;
}
IO::SDK::IlluminationAngle& IO::SDK::IlluminationAngle::Incidence()
{
    return _Incidence;
}
IO::SDK::IlluminationAngle& IO::SDK::IlluminationAngle::Emission()
{
    return _Emission;
}