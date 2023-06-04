/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SITE_FRAME_H
#define SITE_FRAME_H

#include <FrameFile.h>

namespace IO::Astrodynamics::Sites {
    class Site;
}

namespace IO::Astrodynamics::Frames {
    /**
     * @brief Site frame file
     * 
     */
    class SiteFrameFile final : public FrameFile {
    private:
        const IO::Astrodynamics::Sites::Site &m_site;

        void BuildFrame();

    public:
        /**
         * @brief Construct a new Site Frame File object
         * 
         * @param site 
         */
        explicit SiteFrameFile(const IO::Astrodynamics::Sites::Site &site);
    };
}

#endif