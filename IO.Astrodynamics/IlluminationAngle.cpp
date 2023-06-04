/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include<IlluminationAngle.h>

#include <utility>
#include <SDKException.h>

IO::Astrodynamics::IlluminationAngle IO::Astrodynamics::IlluminationAngle::mPhase(std::string("PHASE"));
IO::Astrodynamics::IlluminationAngle IO::Astrodynamics::IlluminationAngle::mIncidence(std::string("INCIDENCE"));
IO::Astrodynamics::IlluminationAngle IO::Astrodynamics::IlluminationAngle::mEmission(std::string("EMISSION"));

/**
 * @brief Construct a new IO::Astrodynamics::IlluminationAngle::IlluminationAngle object
 * 
 * @param name 
 */
IO::Astrodynamics::IlluminationAngle::IlluminationAngle(std::string  name):m_name{std::move(name)}
{

}

/**
 * @brief Get illumination angle char array
 * 
 * @return const char* 
 */
const char *IO::Astrodynamics::IlluminationAngle::ToCharArray() const
{
    return m_name.c_str();
}

IO::Astrodynamics::IlluminationAngle& IO::Astrodynamics::IlluminationAngle::Phase()
{
    return mPhase;
}
IO::Astrodynamics::IlluminationAngle& IO::Astrodynamics::IlluminationAngle::Incidence()
{
    return mIncidence;
}
IO::Astrodynamics::IlluminationAngle& IO::Astrodynamics::IlluminationAngle::Emission()
{
    return mEmission;
}

IO::Astrodynamics::IlluminationAngle IO::Astrodynamics::IlluminationAngle::ToIlluminationAngleType(const std::string &illuminationAngleType)
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

    throw IO::Astrodynamics::Exception::SDKException("Invalid illumination type : " + illuminationAngleType);
}
