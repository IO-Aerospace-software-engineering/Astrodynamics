//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_BODYVISIBILITYPARAMETERS_H
#define IOSDK_BODYVISIBILITYPARAMETERS_H

#include <TDB.h>
#include <Window.h>
#include <Aberrations.h>
#include <Body.h>

namespace IO::SDK::Constraints::Parameters
{

    class BodyVisibilityParameters
    {
    private:
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> &m_window;
        const IO::SDK::Body::Body &m_target;
        const IO::SDK::AberrationsEnum m_aberration;

    public:
        size_t Order{};

        BodyVisibilityParameters(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window, const IO::SDK::Body::Body target, const IO::SDK::AberrationsEnum aberration);

        inline bool operator<(const BodyVisibilityParameters &rhs) const
        { return Order < rhs.Order; }
    };

} // IO

#endif //IOSDK_BODYVISIBILITYPARAMETERS_H
