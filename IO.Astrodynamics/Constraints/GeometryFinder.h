/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 20/04/2023.
//

#ifndef IOSDK_GEOMETRYFINDER_H
#define IOSDK_GEOMETRYFINDER_H


#include <vector>

#include <Aberrations.h>
#include <RelationalOperator.h>
#include <Window.h>
#include <OccultationType.h>
#include <CoordinateSystem.h>
#include <Coordinate.h>
#include <Geodetic.h>
#include <Illumination.h>
#include "IlluminationAngle.h"

namespace IO::SDK::Constraints
{

    class GeometryFinder
    {
    public:
        static std::vector<Time::Window<Time::TDB>>
        FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, int observerId, int targetId,
                                        const Constraints::RelationalOperator &constraint, double value, IO::SDK::AberrationsEnum aberration,
                                        const Time::TimeSpan &stepSize);

        static std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
        FindWindowsOnOccultationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, int observerId,
                                           int targetId, const std::string &targetFrame,
                                           const std::string &targetShape,
                                           int frontBodyId, const std::string &frontFrame, const std::string &frontShape,
                                           const IO::SDK::OccultationType &occultationType,
                                           IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize);

        static std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
        FindWindowsOnCoordinateConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, int observerId,
                                          int targetId, const std::string &frame, const IO::SDK::CoordinateSystem &coordinateSystem,
                                          const IO::SDK::Coordinate &coordinate, const IO::SDK::Constraints::RelationalOperator &relationalOperator,
                                          double value, double adjustValue, IO::SDK::AberrationsEnum aberration,
                                          const IO::SDK::Time::TimeSpan &stepSize);

        static std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
        FindWindowsOnIlluminationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, int observerId,
                                            const std::string &illuminationSource, int targetBody, const std::string &fixedFrame,
                                            const double coordinates[3], const IlluminationAngle &illuminationType,
                                            const IO::SDK::Constraints::RelationalOperator &relationalOperator, double value, double adjustValue,
                                            IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize,
                                            const std::string &method
        );

        static std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
        FindWindowsInFieldOfViewConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, int observerId,int instrumentId,
                                           int targetId, const std::string &targetFrame,
                                           const std::string &targetShape,
                                           IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize);

    };

}

#endif //IOSDK_GEOMETRYFINDER_H
