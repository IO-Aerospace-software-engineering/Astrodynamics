/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <InertialFrames.h>

IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::mICRF(std::string("J2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::mECLIPTIC(std::string("ECLIPJ2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::mGALACTIC(std::string("GALACTIC"));
IO::SDK::Frames::InertialFrames::InertialFrames(const std::string &name) : IO::SDK::Frames::Frames::Frames(name)
{
}

IO::SDK::Frames::InertialFrames& IO::SDK::Frames::InertialFrames::GetICRF()
{
    return IO::SDK::Frames::InertialFrames::mICRF;
}

IO::SDK::Frames::InertialFrames& IO::SDK::Frames::InertialFrames::Galactic()
{
    return IO::SDK::Frames::InertialFrames::mGALACTIC;
}

IO::SDK::Frames::InertialFrames& IO::SDK::Frames::InertialFrames::Ecliptic()
{
    return IO::SDK::Frames::InertialFrames::mECLIPTIC;
}