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

namespace IO::Astrodynamics::Constraints
{

    class GeometryFinder
    {
    public:
        static std::vector<Time::Window<Time::TDB>>
        FindWindowsOnDistanceConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, int observerId, int targetId,
                                        const Constraints::RelationalOperator &constraint, double value, IO::Astrodynamics::AberrationsEnum aberration,
                                        const Time::TimeSpan &stepSize);

        static std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsOnOccultationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, int observerId,
                                           int targetId, const std::string &targetFrame,
                                           const std::string &targetShape,
                                           int frontBodyId, const std::string &frontFrame, const std::string &frontShape,
                                           const IO::Astrodynamics::OccultationType &occultationType,
                                           IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TimeSpan &stepSize);

        static std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsOnCoordinateConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, int observerId,
                                          int targetId, const std::string &frame, const IO::Astrodynamics::CoordinateSystem &coordinateSystem,
                                          const IO::Astrodynamics::Coordinate &coordinate, const IO::Astrodynamics::Constraints::RelationalOperator &relationalOperator,
                                          double value, double adjustValue, IO::Astrodynamics::AberrationsEnum aberration,
                                          const IO::Astrodynamics::Time::TimeSpan &stepSize);

        static std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsOnIlluminationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, int observerId,
                                            const std::string &illuminationSource, int targetBody, const std::string &fixedFrame,
                                            const double coordinates[3], const IlluminationAngle &illuminationType,
                                            const IO::Astrodynamics::Constraints::RelationalOperator &relationalOperator, double value, double adjustValue,
                                            IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TimeSpan &stepSize,
                                            const std::string &method
        );

        static std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsInFieldOfViewConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, int observerId,int instrumentId,
                                           int targetId, const std::string &targetFrame,
                                           const std::string &targetShape,
                                           IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TimeSpan &stepSize);

    };

}

#endif //IOSDK_GEOMETRYFINDER_H
