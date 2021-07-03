/**
 * @file BodyFixedFrames.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
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
        BodyFixedFrames(const std::string &name);
        
    };
}
#endif // ! INERTIAL_FRAMES_H