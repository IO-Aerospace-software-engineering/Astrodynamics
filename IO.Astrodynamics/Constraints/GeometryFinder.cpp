/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 20/04/2023.
//

#include <GeometryFinder.h>
#include <Builder.h>

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, int observerId,
                                                                                int targetId,
                                                                                const Constraints::RelationalOperator &constraint, const double value,
                                                                                const IO::Astrodynamics::AberrationsEnum aberration,
                                                                                const Time::TimeSpan &stepSize)
{
    std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt NINTVL{10000};
    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_DIST[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_DIST);

    SpiceDouble SPICE_CELL_DIST_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_DIST_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfdist_c(std::to_string(targetId).c_str(), IO::Astrodynamics::Aberrations::ToString(aberration).c_str(), std::to_string(observerId).c_str(), constraint.ToCharArray(),
             value * 1E-03, 0.0,
             stepSize.GetSeconds().count(),
             NINTVL, &cnfine, &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowStart)),
                             IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnOccultationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow,
                                                                                   int observerId,
                                                                                   int targetBodyId, const std::string &targetFrame,
                                                                                   const std::string &targetShape,
                                                                                   int frontBodyId, const std::string &frontFrame, const std::string &frontShape,
                                                                                   const IO::Astrodynamics::OccultationType &occultationType,
                                                                                   IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TimeSpan &stepSize)
{
    std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    std::string computedFontShape{"ELLIPSOID"};
    if (!frontShape.empty())
    {
        computedFontShape = frontShape;
    }

    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

    SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfoclt_c(occultationType.ToCharArray(), std::to_string(frontBodyId).c_str(), "ELLIPSOID", frontFrame.c_str(), std::to_string(targetBodyId).c_str(), targetShape.c_str(),
             targetFrame.c_str(), IO::Astrodynamics::Aberrations::ToString(aberration).c_str(), std::to_string(observerId).c_str(), stepSize.GetSeconds().count(), &cnfine,
             &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowStart)),
                             IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnCoordinateConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, int observerId,
                                                                                  int targetId, const std::string &frame,
                                                                                  const IO::Astrodynamics::CoordinateSystem &coordinateSystem,
                                                                                  const IO::Astrodynamics::Coordinate &coordinate,
                                                                                  const IO::Astrodynamics::Constraints::RelationalOperator &relationalOperator,
                                                                                  double value, double adjustValue, IO::Astrodynamics::AberrationsEnum aberration,
                                                                                  const IO::Astrodynamics::Time::TimeSpan &stepSize)
{
    std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt NINTVL{10000};
    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

    SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfposc_c(std::to_string(targetId).c_str(), frame.c_str(), IO::Astrodynamics::Aberrations::ToString(aberration).c_str(), std::to_string(observerId).c_str(),
             coordinateSystem.ToCharArray(), coordinate.ToCharArray(), relationalOperator.ToCharArray(), value, adjustValue, stepSize.GetSeconds().count(), NINTVL, &cnfine,
             &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(
                IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowStart)),
                IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnIlluminationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow,
                                                                                    int observerId,
                                                                                    const std::string &illuminationSource, int targetBody, const std::string &fixedFrame,
                                                                                    const double coordinates[3], const IlluminationAngle &illuminationType,
                                                                                    const IO::Astrodynamics::Constraints::RelationalOperator &relationalOperator, double value,
                                                                                    double adjustValue,
                                                                                    IO::Astrodynamics::AberrationsEnum aberration,
                                                                                    const IO::Astrodynamics::Time::TimeSpan &stepSize,
                                                                                    const std::string &method = "Ellipsoid")
{
    std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> windows;


    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt MAXIVL{1000};
    const SpiceInt MAXWIN{2000};

    SpiceDouble SPICE_CELL_A[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_A);

    SpiceDouble SPICE_CELL_B[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_B);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(),
             searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfilum_c(method.c_str(), illuminationType.ToCharArray(), std::to_string(targetBody).c_str(), illuminationSource.c_str(),
             fixedFrame.c_str(), IO::Astrodynamics::Aberrations::ToString(aberration).c_str(),
             std::to_string(observerId).c_str(), coordinates, relationalOperator.ToCharArray(), value, adjustValue, stepSize.GetSeconds().count(),
             MAXIVL, &cnfine, &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(
                IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowStart)),
                IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow,
                                                                                   int observerId, int instrumentId,
                                                                                   int targetId, const std::string &targetFrame, const std::string &targetShape,
                                                                                   IO::Astrodynamics::AberrationsEnum aberration,
                                                                                   const IO::Astrodynamics::Time::TimeSpan &stepSize)
{
    std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

    SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::Astrodynamics::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(),
             searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine

    );

    gftfov_c(std::to_string(instrumentId).c_str(), std::to_string(targetId).c_str(), targetShape.c_str(), targetFrame.c_str(),
             IO::Astrodynamics::Aberrations::ToString(aberration).c_str(),
             std::to_string(observerId).c_str(), stepSize.GetSeconds().count(), &cnfine, &results
    );

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(
                IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowStart)),
                IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(windowEnd)));

    }
    return windows;
}
