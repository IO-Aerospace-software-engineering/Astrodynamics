/**
 * @file SiteFrameFile.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SITE_FRAME_H
#define SITE_FRAME_H

#include <FrameFile.h>

namespace IO::SDK::Sites
{
    class Site;
}

namespace IO::SDK::Frames
{
    /**
     * @brief Site frame file
     * 
     */
    class SiteFrameFile final : public FrameFile
    {
    private:
        const IO::SDK::Sites::Site &m_site;
        void BuildFrame();

    public:
        /**
         * @brief Construct a new Site Frame File object
         * 
         * @param site 
         */
        SiteFrameFile(const IO::SDK::Sites::Site &site);
    };
}

#endif