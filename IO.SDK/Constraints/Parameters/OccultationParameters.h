//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_OCCULTATIONPARAMETERS_H
#define IOSDK_OCCULTATIONPARAMETERS_H

#include <CelestialBody.h>

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
                              IO::SDK::AberrationsEnum aberration,
                              const IO::SDK::Time::TimeSpan &initialStepSize);

        [[nodiscard]] inline const IO::SDK::Body::Body &GetObserver() const
        { return m_observer; }

        [[nodiscard]] inline const IO::SDK::Body::CelestialBody &GetFront() const
        { return m_front; }

        [[nodiscard]] inline const IO::SDK::Body::Body &GetBack() const
        { return m_back; }

        [[nodiscard]] inline const IO::SDK::OccultationType &GetOccultationType() const
        { return m_occultationType; }

        [[nodiscard]] inline IO::SDK::AberrationsEnum GetAberration() const
        { return m_aberration; }

        [[nodiscard]] inline const IO::SDK::Time::TimeSpan &GetInitialStepSize() const
        { return m_initialStepSize; }

    };
}

#endif //IOSDK_OCCULTATIONPARAMETERS_H
