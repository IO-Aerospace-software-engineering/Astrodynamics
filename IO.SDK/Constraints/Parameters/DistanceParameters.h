//
// Created by s.guillet on 27/02/2023.
//

#ifndef IOSDK_DISTANCEPARAMETERS_H
#define IOSDK_DISTANCEPARAMETERS_H

#include <Body.h>
#include <CelestialBody.h>
#include <Constraint.h>
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
        const IO::SDK::Constraints::Constraint &m_constraint;
        const IO::SDK::AberrationsEnum m_aberration;
        const double m_value;
        const IO::SDK::Time::TimeSpan &m_initialStepSize;

    public:
        DistanceParameters(const IO::SDK::Body::Body &observer,
                           const IO::SDK::Body::Body &target,
                           const IO::SDK::Constraints::Constraint &constraint,
                           const IO::SDK::AberrationsEnum aberration,
                           const double value,
                           const IO::SDK::Time::TimeSpan &initialStepSize);

        inline const IO::SDK::Body::Body &GetObserver()
        { return m_observer; }

        inline const IO::SDK::Body::Body &GetTarget()
        { return m_target; }

        inline const IO::SDK::Constraints::Constraint &GetConstraint()
        { return m_constraint; }

        inline const IO::SDK::AberrationsEnum &GetAberration()
        { return m_aberration; }

        inline const double GetValue()
        { return m_value; }

        inline const IO::SDK::Time::TimeSpan &GetInitialStepSize()
        { return m_initialStepSize; }

    };


} // IO

#endif //IOSDK_DISTANCEPARAMETERS_H
