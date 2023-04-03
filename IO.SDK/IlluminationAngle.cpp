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

#include <utility>

IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::mPhase(std::string("PHASE"));
IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::mIncidence(std::string("INCIDENCE"));
IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::mEmission(std::string("EMISSION"));

/**
 * @brief Construct a new IO::SDK::IlluminationAngle::IlluminationAngle object
 * 
 * @param name 
 */
IO::SDK::IlluminationAngle::IlluminationAngle(std::string  name):m_name{std::move(name)}
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
    return mPhase;
}
IO::SDK::IlluminationAngle& IO::SDK::IlluminationAngle::Incidence()
{
    return mIncidence;
}
IO::SDK::IlluminationAngle& IO::SDK::IlluminationAngle::Emission()
{
    return mEmission;
}