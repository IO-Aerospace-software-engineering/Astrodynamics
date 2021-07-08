/**
 * @file InertialFrames.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "InertialFrames.h"

IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::_ICRF(std::string("J2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::_ECLIPTIC(std::string("ECLIPJ2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::_GALACTIC(std::string("GALACTIC"));
IO::SDK::Frames::InertialFrames::InertialFrames(const std::string &name) : IO::SDK::Frames::Frames::Frames(name)
{
}

IO::SDK::Frames::InertialFrames& IO::SDK::Frames::InertialFrames::GetICRF()
{
    return IO::SDK::Frames::InertialFrames::_ICRF;
}

IO::SDK::Frames::InertialFrames& IO::SDK::Frames::InertialFrames::Galactic()
{
    return IO::SDK::Frames::InertialFrames::_GALACTIC;
}

IO::SDK::Frames::InertialFrames& IO::SDK::Frames::InertialFrames::Ecliptic()
{
    return IO::SDK::Frames::InertialFrames::_ECLIPTIC;
}