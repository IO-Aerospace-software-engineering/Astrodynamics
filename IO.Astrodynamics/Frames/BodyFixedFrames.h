/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef BODY_FIXED_FRAMES_H
#define BODY_FIXED_FRAMES_H
#include <string>
#include <Frames.h>

namespace IO::Astrodynamics::Frames
{
    /**
     * @brief Body fixed frames
     * 
     */
    class BodyFixedFrames final : public IO::Astrodynamics::Frames::Frames
    {

    private:
        static BodyFixedFrames mTeme;
    public:
        explicit BodyFixedFrames(const std::string &name);

        static IO::Astrodynamics::Frames::BodyFixedFrames& TEME();

        
    };
}
#endif // ! BODY_FIXED_FRAMES_H