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
        const IO::SDK::Body::Body &m_observer;
        const IO::SDK::Body::CelestialBody &m_front;
        const IO::SDK::Body::Body &m_back;
        const IO::SDK::OccultationType &m_occultationType;
        const IO::SDK::AberrationsEnum m_aberration;
        const IO::SDK::Time::TimeSpan &m_initialStepSize;

    public:
        OccultationParameters(const IO::SDK::Body::Body &observer,
                              const IO::SDK::Body::CelestialBody &front,
                              const IO::SDK::Body::Body &back,
                              const IO::SDK::OccultationType &occultationType,
                              const IO::SDK::AberrationsEnum aberration,
                              const IO::SDK::Time::TimeSpan &initialStepSize);

        inline const IO::SDK::Body::Body &GetObserver() const
        { return m_observer; }

        inline const IO::SDK::Body::CelestialBody &GetFront() const
        { return m_front; }

        inline const IO::SDK::Body::Body &GetBack() const
        { return m_back; }

        inline const IO::SDK::OccultationType &GetOccultationType() const
        { return m_occultationType; }

        inline const IO::SDK::AberrationsEnum GetAberration() const
        { return m_aberration; }

        inline const IO::SDK::Time::TimeSpan &GetInitialStepSize() const
        { return m_initialStepSize; }

    };
}

#endif //IOSDK_OCCULTATIONPARAMETERS_H
