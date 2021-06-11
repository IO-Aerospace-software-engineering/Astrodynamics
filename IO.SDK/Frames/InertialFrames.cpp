#include "InertialFrames.h"

IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::ICRF(std::string("J2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::ECLIPTIC(std::string("ECLIPJ2000"));
IO::SDK::Frames::InertialFrames IO::SDK::Frames::InertialFrames::GALACTIC(std::string("GALACTIC"));
IO::SDK::Frames::InertialFrames::InertialFrames(const std::string &name) : IO::SDK::Frames::Frames::Frames(name)
{
}
