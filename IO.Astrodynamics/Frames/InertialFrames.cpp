/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <InertialFrames.h>

IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mICRF(std::string("J2000"));
IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mECLIPTIC_J2000(std::string("ECLIPJ2000"));
IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mECLIPTIC_B1950(std::string("ECLIPB1950"));
IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mB1950(std::string("B1950"));
IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mGALACTIC(std::string("GALACTIC"));
IO::Astrodynamics::Frames::InertialFrames IO::Astrodynamics::Frames::InertialFrames::mFK4(std::string("FK4"));
IO::Astrodynamics::Frames::InertialFrames::InertialFrames(const std::string &name) : IO::Astrodynamics::Frames::Frames::Frames(name)
{
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::ICRF()
{
    return IO::Astrodynamics::Frames::InertialFrames::mICRF;
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::Galactic()
{
    return IO::Astrodynamics::Frames::InertialFrames::mGALACTIC;
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::EclipticJ2000()
{
    return IO::Astrodynamics::Frames::InertialFrames::mECLIPTIC_J2000;
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::EclipticB1950()
{
    return IO::Astrodynamics::Frames::InertialFrames::mECLIPTIC_B1950;
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::B1950()
{
    return IO::Astrodynamics::Frames::InertialFrames::mB1950;
}

IO::Astrodynamics::Frames::InertialFrames& IO::Astrodynamics::Frames::InertialFrames::FK4()
{
    return IO::Astrodynamics::Frames::InertialFrames::mFK4;
}