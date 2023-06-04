/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SITE_FRAME_H
#define SITE_FRAME_H

#include <FrameFile.h>

namespace IO::SDK::Sites {
    class Site;
}

namespace IO::SDK::Frames {
    /**
     * @brief Site frame file
     * 
     */
    class SiteFrameFile final : public FrameFile {
    private:
        const IO::SDK::Sites::Site &m_site;

        void BuildFrame();

    public:
        /**
         * @brief Construct a new Site Frame File object
         * 
         * @param site 
         */
        explicit SiteFrameFile(const IO::SDK::Sites::Site &site);
    };
}

#endif