/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include<IlluminationAngle.h>

#include <utility>
#include <SDKException.h>

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

IO::SDK::IlluminationAngle IO::SDK::IlluminationAngle::ToIlluminationAngleType(const std::string &illuminationAngleType)
{
    if (illuminationAngleType == IlluminationAngle::mPhase.ToCharArray())
    {
        return mPhase;
    } else if (illuminationAngleType == IlluminationAngle::mIncidence.ToCharArray())
    {
        return mIncidence;
    } else if (illuminationAngleType == IlluminationAngle::mEmission.ToCharArray())
    {
        return mEmission;
    }

    throw IO::SDK::Exception::SDKException("Invalid illumination type : " + illuminationAngleType);
}
