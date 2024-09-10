/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <BodyFixedFrames.h>

IO::Astrodynamics::Frames::BodyFixedFrames IO::Astrodynamics::Frames::BodyFixedFrames::mTeme(std::string("TEME"));

IO::Astrodynamics::Frames::BodyFixedFrames::BodyFixedFrames(const std::string &name) : IO::Astrodynamics::Frames::Frames::Frames(name)
{
}

IO::Astrodynamics::Frames::BodyFixedFrames& IO::Astrodynamics::Frames::BodyFixedFrames::TEME()
{
 return IO::Astrodynamics::Frames::BodyFixedFrames::mTeme;
}