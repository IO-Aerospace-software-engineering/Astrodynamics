//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_OCCULTATIONPARAMETERS_H
#define IOSDK_OCCULTATIONPARAMETERS_H

#include <Aberrations.h>
#include <CelestialBody.h>
#include <OccultationType.h>
#include <TDB.h>
#include <Window.h>

namespace IO::SDK::Constraints::Parameters
{
    class OccultationParameters
    {
    private:
        const IO::SDK::Time::Window<IO::SDK::Time::TDB> m_window;
        const IO::SDK::Body::Body &m_observer;
        const IO::SDK::Body::CelestialBody &m_front;
        const IO::SDK::Body::Body &m_back;
        const IO::SDK::OccultationType &m_occultationType;
        const IO::SDK::AberrationsEnum m_aberration;
        const IO::SDK::Time::TimeSpan &m_initialStepSize;

    public:
        OccultationParameters(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &window,
                              const IO::SDK::Body::Body &observer,
                              const IO::SDK::Body::CelestialBody &front,
                              const IO::SDK::Body::Body &back,
                              const IO::SDK::OccultationType &occultationType,
                              const IO::SDK::AberrationsEnum aberration,
                              const IO::SDK::Time::TimeSpan &initialStepSize);
        int Order{};
        inline bool operator<(const OccultationParameters &rhs) const
        { return Order < rhs.Order; }
    };
}

#endif //IOSDK_OCCULTATIONPARAMETERS_H
