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

IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::ICRF(std::string("J2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::ECLIPTIC(std::string("ECLIPJ2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::GALACTIC(std::string("GALACTIC"));
IO::SDK::Frames::InertialFrames::InertialFrames(const std::string &name) : IO::SDK::Frames::Frames::Frames(name)
{
}
