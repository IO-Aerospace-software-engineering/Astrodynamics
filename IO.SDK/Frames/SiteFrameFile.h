#ifndef SITE_FRAME_H
#define SITE_FRAME_H

#include <FrameFile.h>

namespace IO::SDK::Sites
{
    class Site;
}

namespace IO::SDK::Frames
{
    class SiteFrameFile final : public FrameFile
    {
    private:
        const IO::SDK::Sites::Site &m_site;
        void BuildFrame();

    public:
        SiteFrameFile(const IO::SDK::Sites::Site &site);
    };
}

#endif