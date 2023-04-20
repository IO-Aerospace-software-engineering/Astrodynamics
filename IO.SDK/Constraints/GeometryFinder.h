//
// Created by s.guillet on 20/04/2023.
//

#ifndef IOSDK_GEOMETRYFINDER_H
#define IOSDK_GEOMETRYFINDER_H


#include <vector>

#include <Aberrations.h>
#include <Constraint.h>
#include <Window.h>
#include <OccultationType.h>

namespace IO::SDK::Constraints
{

    class GeometryFinder
    {
    public:
        static std::vector<Time::Window<Time::TDB>>
        FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const std::string &target, const std::string &observer,
                                        const Constraints::Constraint &constraint, double value, IO::SDK::AberrationsEnum aberration,
                                        const Time::TimeSpan &stepSize);

        static std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
        FindWindowsOnOccultationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const std::string &observer,
                                           const std::string &targetBody, const std::string &targetFrame,
                                           const std::string &targetShape,
                                           const std::string &frontBody, const std::string &frontFrame, const std::string &frontShape,
                                           const IO::SDK::OccultationType &occultationType,
                                           IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize);

    };

}

#endif //IOSDK_GEOMETRYFINDER_H
