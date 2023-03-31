//
// Created by spacer on 3/7/23.
//

#ifndef IOSDK_INFIELDOFVIEWPARAMETERS_H
#define IOSDK_INFIELDOFVIEWPARAMETERS_H

#include <Body.h>
#include <Instrument.h>
#include <Aberrations.h>
#include <TimeSpan.h>

namespace IO::SDK::Instruments
{
    class Instrument;
}

namespace IO::SDK::Constraints::Parameters
{
    class InFieldOfViewParameters
    {
    private:
        const IO::SDK::Instruments::Instrument &m_instrument;
        const IO::SDK::Body::Body &m_targetBody;
        const IO::SDK::AberrationsEnum m_aberration;
        const IO::SDK::Time::TimeSpan &m_initialStepSize;

    public:
        InFieldOfViewParameters(const SDK::Instruments::Instrument &instrument, const SDK::Body::Body &targetBody,
                                IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &initialStepSize);

        [[nodiscard]] inline const IO::SDK::Instruments::Instrument &GetInstrument() const
        { return m_instrument; }

        [[nodiscard]] inline const IO::SDK::Body::Body &GetTargetBody() const
        { return m_targetBody; }

        inline IO::SDK::AberrationsEnum GetAberration()
        { return m_aberration; }

        inline const IO::SDK::Time::TimeSpan &GetInitialStepSize()
        { return m_initialStepSize; }
    };
} // IO

#endif //IOSDK_INFIELDOFVIEWPARAMETERS_H
