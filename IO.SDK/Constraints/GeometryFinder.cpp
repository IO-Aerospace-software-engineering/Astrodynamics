//
// Created by s.guillet on 20/04/2023.
//

#include <GeometryFinder.h>
#include <SpiceUsr.h>
#include <Builder.h>
#include "CelestialBody.h"
#include "IlluminationAngle.h"
#include <Helpers/Type.cpp>

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const std::string &observer,
                                                                      const std::string &target,
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

    std::string computedFontShape{"ELLIPSOID"};
    if (!frontShape.empty())
    {
        computedFontShape = frontShape;
    }

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

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Constraints::GeometryFinder::FindWindowsOnCoordinateConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const std::string &observer,
                                                                        const std::string &target, const std::string &frame, const IO::SDK::CoordinateSystem &coordinateSystem,
                                                                        const IO::SDK::Coordinate &coordinate, const IO::SDK::Constraints::Constraint &relationalOperator,
                                                                        double value, double adjustValue, IO::SDK::AberrationsEnum aberration,
                                                                        const IO::SDK::Time::TimeSpan &stepSize)
{
    std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt NINTVL{10000};
    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

    SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfposc_c(target.c_str(), frame.c_str(), IO::SDK::Aberrations::ToString(aberration).c_str(), observer.c_str(),
             coordinateSystem.ToCharArray(), coordinate.ToCharArray(), relationalOperator.ToCharArray(), value, adjustValue, stepSize.GetSeconds().count(), NINTVL, &cnfine,
             &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)),
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Constraints::GeometryFinder::FindWindowsOnIlluminationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const std::string &observer,
                                                                          const std::string &illuminationSource, int targetBody, const std::string &fixedFrame,
                                                                          const double coordinates[3], const IlluminationAngle &illuminationType,
                                                                          const IO::SDK::Constraints::Constraint &relationalOperator, double value, double adjustValue,
                                                                          IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize,
                                                                          const std::string &method = "Ellipsoid")
{
    std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> windows;

//    IO::SDK::Body::CelestialBody body(targetBody);
//    SpiceDouble bodyFixedLocation[3];
//    georec_c(coordinates.GetLongitude(), coordinates.GetLatitude(), coordinates.GetAltitude(),
//             body.GetRadius().GetX(), body.GetFlattening(), bodyFixedLocation);

    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    const SpiceInt MAXIVL{1000};
    const SpiceInt MAXWIN{2000};

    SpiceDouble SPICE_CELL_A[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_A);

    SpiceDouble SPICE_CELL_B[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_B);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(),
             searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfilum_c(method.c_str(), illuminationType.ToCharArray(), std::to_string(targetBody).c_str(), illuminationSource.c_str(),
             fixedFrame.c_str(), IO::SDK::Aberrations::ToString(aberration).c_str(),
             observer.c_str(), coordinates, relationalOperator.ToCharArray(), value, adjustValue, stepSize.GetSeconds().count(),
             MAXIVL, &cnfine, &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.emplace_back(
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)),
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd)));
    }
    return windows;
}
