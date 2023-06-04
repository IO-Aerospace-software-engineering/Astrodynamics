/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef BODY_FIXED_FRAMES_H
#define BODY_FIXED_FRAMES_H
#include <string>
#include <Frames.h>

namespace IO::SDK::Frames
{
    /**
     * @brief Body fixed frames
     * 
     */
    class BodyFixedFrames final : public IO::SDK::Frames::Frames
    {

    private:
    public:
        explicit BodyFixedFrames(const std::string &name);
        
    };
}
#endif // ! BODY_FIXED_FRAMES_H