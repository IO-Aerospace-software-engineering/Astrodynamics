/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <InertialFrames.h>

IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mICRF(std::string("J2000"));
IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mECLIPTIC(std::string("ECLIPJ2000"));
IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mGALACTIC(std::string("GALACTIC"));
IO::Astrodynamics::Frames::InertialFrames::InertialFrames(const std::string &name) : IO::Astrodynamics::Frames::Frames::Frames(name)
{
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::GetICRF()
{
    return IO::Astrodynamics::Frames::InertialFrames::mICRF;
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::Galactic()
{
    return IO::Astrodynamics::Frames::InertialFrames::mGALACTIC;
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::Ecliptic()
{
    return IO::Astrodynamics::Frames::InertialFrames::mECLIPTIC;
}

