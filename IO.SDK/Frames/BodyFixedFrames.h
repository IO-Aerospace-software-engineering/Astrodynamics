#ifndef BODY_FIXED_FRAMES_H
#define BODY_FIXED_FRAMES_H
#include <string>
#include <Frames.h>

namespace IO::SDK::Frames
{

    class BodyFixedFrames final : public IO::SDK::Frames::Frames
    {

    private:
    public:
        BodyFixedFrames(const std::string &name);
        
    };
}
#endif // ! INERTIAL_FRAMES_H