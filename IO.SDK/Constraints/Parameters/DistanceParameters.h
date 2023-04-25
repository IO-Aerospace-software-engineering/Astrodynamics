//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_DISTANCEPARAMETERS_H
#define IOSDK_DISTANCEPARAMETERS_H

#include <Body.h>
#include <CelestialBody.h>
#include <RelationalOperator.h>
#include <Aberrations.h>
#include <Window.h>
#include <TDB.h>

namespace IO::SDK::Constraints::Parameters
{

    class DistanceParameters
    {
    private:

        const IO::SDK::Body::Body &m_observer;
        const IO::SDK::Body::Body &m_target;
        const IO::SDK::Constraints::RelationalOperator &m_constraint;
        const IO::SDK::AberrationsEnum m_aberration;
        const double m_value;
        const IO::SDK::Time::TimeSpan &m_initialStepSize;

    public:
        DistanceParameters(const IO::SDK::Body::Body &observer,
                           const IO::SDK::Body::Body &target,
                           const IO::SDK::Constraints::RelationalOperator &constraint,
                           IO::SDK::AberrationsEnum aberration,
                           double value,
                           const IO::SDK::Time::TimeSpan &initialStepSize);

        [[nodiscard]] inline const IO::SDK::Body::Body &GetObserver() const
        { return m_observer; }

        [[nodiscard]] inline const IO::SDK::Body::Body &GetTarget() const
        { return m_target; }

        [[nodiscard]] inline const IO::SDK::Constraints::RelationalOperator &GetConstraint() const
        { return m_constraint; }

        [[nodiscard]] inline const IO::SDK::AberrationsEnum &GetAberration() const
        { return m_aberration; }

        [[nodiscard]] inline double GetValue() const
        { return m_value; }

        [[nodiscard]] inline const IO::SDK::Time::TimeSpan &GetInitialStepSize() const
        { return m_initialStepSize; }

    };


} // IO

#endif //IOSDK_DISTANCEPARAMETERS_H
