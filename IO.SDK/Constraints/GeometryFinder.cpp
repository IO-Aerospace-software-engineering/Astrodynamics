//
// Created by s.guillet on 20/04/2023.
//

#include <GeometryFinder.h>
#include <SpiceUsr.h>
#include <Builder.h>
#include "CelestialBody.h"
#include <Helpers/Type.cpp>

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const std::string &target,
                                                                      const std::string &observer,
                                                                      const Constraints::Constraint &constraint, const double value, const IO::SDK::AberrationsEnum aberration,
                                                                      const Time::TimeSpan &stepSize)
{
    std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt NINTVL{10000};
    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_DIST[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_DIST);

    SpiceDouble SPICE_CELL_DIST_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_DIST_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfdist_c(target.c_str(), IO::SDK::Aberrations::ToString(aberration).c_str(), observer.c_str(), constraint.ToCharArray(), value * 1E-03, 0.0,
             stepSize.GetSeconds().count(),
             NINTVL, &cnfine, &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)),
                             IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Constraints::GeometryFinder::FindWindowsOnOccultationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const std::string &observer,
                                                                         const std::string &targetBody, const std::string &targetFrame,
                                                                         const std::string &targetShape,
                                                                         const std::string &frontBody, const std::string &frontFrame, const std::string &frontShape,
                                                                         const IO::SDK::OccultationType &occultationType,
                                                                         IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize)
{
    std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

    SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfoclt_c(occultationType.ToCharArray(), frontBody.c_str(), "ELLIPSOID", frontFrame.c_str(), targetBody.c_str(), targetShape.c_str(),
             targetFrame.c_str(), IO::SDK::Aberrations::ToString(aberration).c_str(), observer.c_str(), stepSize.GetSeconds().count(), &cnfine, &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)),
                             IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}