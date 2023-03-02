/**
 * @file Site.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <cmath>

#include <SDKException.h>
#include <Site.h>
#include <Constants.h>
#include <InertialFrames.h>
#include <StateVector.h>
#include <SpiceUsr.h>
#include <SiteFrameFile.h>
#include <Builder.h>
#include <CoordinateSystem.h>
#include <Coordinate.h>


using namespace std::chrono_literals;

IO::SDK::Sites::Site::Site(const int id, const IO::SDK::Scenario &scenario, const std::string &name, const IO::SDK::Coordinates::Geodetic &coordinates,
                           std::shared_ptr<IO::SDK::Body::CelestialBody> &body) : m_id{id}, m_scenario{scenario},
                                                                                  m_name{name},
                                                                                  m_coordinates{coordinates},
                                                                                  m_filePath{std::string(IO::SDK::Parameters::SitePath) + "/" + name},
                                                                                  m_body{body},
                                                                                  m_ephemerisKernel{std::make_unique<IO::SDK::Kernels::EphemerisKernel>(*this)},
                                                                                  m_frame{std::make_unique<IO::SDK::Frames::SiteFrameFile>(*this)}
{
}

IO::SDK::OrbitalParameters::StateVector
IO::SDK::Sites::Site::GetStateVector(const IO::SDK::Frames::Frames frame, const IO::SDK::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius() * 1000.0;
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(), radius.GetX(),
             m_body->GetFlattening(), bodyFixedLocation);
    IO::SDK::OrbitalParameters::StateVector siteVectorState{m_body, IO::SDK::Math::Vector3D(bodyFixedLocation[0],
                                                                                            bodyFixedLocation[1],
                                                                                            bodyFixedLocation[2]),
                                                            IO::SDK::Math::Vector3D(), epoch,
                                                            m_body->GetBodyFixedFrame()};

    return siteVectorState.ToFrame(frame);
}

IO::SDK::Coordinates::RADec
IO::SDK::Sites::Site::GetRADec(const IO::SDK::Body::Body &body, const IO::SDK::AberrationsEnum aberrationCorrection,
                               const IO::SDK::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius();
    auto bodiesSv = body.ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), aberrationCorrection, epoch,
                                       *m_body);

    auto siteVector = GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), epoch);

    auto resultSv = bodiesSv.GetPosition() - siteVector.GetPosition();

    ConstSpiceDouble rectan[3]{resultSv.GetX(), resultSv.GetY(), resultSv.GetZ()};
    double r, ra, dec;
    recrad_c(rectan, &r, &ra, &dec);

    return IO::SDK::Coordinates::RADec(ra, dec, r);
}

IO::SDK::Illumination::Illumination
IO::SDK::Sites::Site::GetIllumination(const IO::SDK::AberrationsEnum aberrationCorrection,
                                      const IO::SDK::Time::TDB &epoch) const
{
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(),
             m_body->GetRadius().GetX(), m_body->GetFlattening(), bodyFixedLocation);

    SpiceDouble srfvec[3];
    SpiceDouble emi;
    SpiceDouble pha;
    SpiceDouble inc;
    SpiceDouble srfEpoch;

    Aberrations abe;

    ilumin_c("Ellipsoid", std::to_string(m_body->GetId()).c_str(), epoch.GetSecondsFromJ2000().count(),
             m_body->GetBodyFixedFrame().GetName().c_str(), abe.ToString(aberrationCorrection).c_str(), "10",
             bodyFixedLocation, &srfEpoch, srfvec, &pha, &inc, &emi);

    return IO::SDK::Illumination::Illumination(
            IO::SDK::Math::Vector3D(srfvec[0] * 1000.0, srfvec[1] * 1000.0, srfvec[2] * 1000.0), pha, inc, emi,
            IO::SDK::Time::TDB(std::chrono::duration<double>(srfEpoch)));
}

bool IO::SDK::Sites::Site::IsDay(const IO::SDK::Time::TDB &epoch, const double twilight) const
{
    return GetIllumination(AberrationsEnum::CNS, epoch).GetIncidence() < IO::SDK::Constants::PI2 - twilight;
}

bool IO::SDK::Sites::Site::IsNight(const IO::SDK::Time::TDB &epoch, const double twilight) const
{
    return GetIllumination(AberrationsEnum::CNS, epoch).GetIncidence() >= IO::SDK::Constants::PI2 - twilight;
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
IO::SDK::Sites::Site::FindDayWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                     const double twilight) const
{
    IO::SDK::Body::CelestialBody sun(10, "Sun");
    return FindWindowsOnIlluminationConstraint(searchWindow, sun, IO::SDK::IlluminationAngle::Incidence(),
                                               IO::SDK::Constraints::Constraint::LowerThan(), IO::SDK::Constants::PI2 - twilight);
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
IO::SDK::Sites::Site::FindNightWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                       const double twilight) const
{
    IO::SDK::Body::CelestialBody sun(10, "Sun");
    return FindWindowsOnIlluminationConstraint(searchWindow, sun, IO::SDK::IlluminationAngle::Incidence(),
                                               IO::SDK::Constraints::Constraint::GreaterThan(), IO::SDK::Constants::PI2 - twilight);
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
IO::SDK::Sites::Site::FindWindowsOnIlluminationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                                          const IO::SDK::Body::Body &observerBody,
                                                          const IO::SDK::IlluminationAngle &illuminationAgngle,
                                                          const IO::SDK::Constraints::Constraint &constraint,
                                                          const double value) const
{
    std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> windows;
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(),
             m_body->GetRadius().GetX(), m_body->GetFlattening(), bodyFixedLocation);

    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    Aberrations abe;

    const SpiceInt MAXIVL{1000};
    const SpiceInt MAXWIN{2000};

    SpiceDouble SPICE_CELL_A[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_A);

    SpiceDouble SPICE_CELL_B[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_B);

    wninsd_c(searchWindow.GetStartDate().ToTDB().GetSecondsFromJ2000().count(),
             searchWindow.GetEndDate().ToTDB().GetSecondsFromJ2000().count(), &cnfine);

    gfilum_c("Ellipsoid", illuminationAgngle.ToCharArray(), std::to_string(m_body->GetId()).c_str(), "Sun",
             m_body->GetBodyFixedFrame().GetName().c_str(), abe.ToString(IO::SDK::AberrationsEnum::CNS).c_str(),
             observerBody.GetName().c_str(), bodyFixedLocation, constraint.ToCharArray(), value, 0.0, 4.5 * 60 * 60,
             MAXIVL, &cnfine, &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.push_back(IO::SDK::Time::Window<IO::SDK::Time::UTC>(
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)).ToUTC(),
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd)).ToUTC()));
    }
    return windows;
}

IO::SDK::Coordinates::HorizontalCoordinates IO::SDK::Sites::Site::GetHorizontalCoordinates(const IO::SDK::Body::Body &body, const IO::SDK::AberrationsEnum aberrationCorrection,
                                                                                           const IO::SDK::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius();
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(), radius.GetX(),
             m_body->GetFlattening(), bodyFixedLocation);

    SpiceDouble res[6];
    SpiceDouble lt;
    azlcpo_c("ELLIPSOID", std::to_string(body.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(),
             m_aberrationHelper.ToString(aberrationCorrection).c_str(), false, true, bodyFixedLocation,
             std::to_string(m_body->GetId()).c_str(),
             m_body->GetBodyFixedFrame().GetName().c_str(), res, &lt);

    return IO::SDK::Coordinates::HorizontalCoordinates(res[1], res[2], res[0] * 1000.0);
}

IO::SDK::OrbitalParameters::StateVector
IO::SDK::Sites::Site::GetStateVector(const IO::SDK::Body::Body &body, const IO::SDK::Frames::Frames frame,
                                     const IO::SDK::AberrationsEnum aberrationCorrection,
                                     const IO::SDK::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius();
    auto bodiesSv = body.ReadEphemeris(frame, aberrationCorrection, epoch, *m_body);

    auto siteVector = GetStateVector(frame, epoch);

    return IO::SDK::OrbitalParameters::StateVector(m_body, bodiesSv.GetPosition() - siteVector.GetPosition(),
                                                   bodiesSv.GetVelocity() - siteVector.GetVelocity(), epoch, frame);
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
IO::SDK::Sites::Site::FindBodyVisibilityWindows(const IO::SDK::Body::Body &body,
                                                const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                                const IO::SDK::AberrationsEnum aberrationCorrection)
{
    std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> windows;
    SpiceDouble windowStart;
    SpiceDouble windowEnd;

    Aberrations abe;

    const SpiceInt NINTVL{10000};
    const SpiceInt MAXWIN{20000};

    SpiceDouble SPICE_CELL_OCCLT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell cnfine = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT);

    SpiceDouble SPICE_CELL_OCCLT_RESULT[SPICE_CELL_CTRLSZ + MAXWIN];
    SpiceCell results = IO::SDK::Spice::Builder::CreateDoubleCell(MAXWIN, SPICE_CELL_OCCLT_RESULT);

    wninsd_c(searchWindow.GetStartDate().GetSecondsFromJ2000().count(), searchWindow.GetEndDate().GetSecondsFromJ2000().count(), &cnfine);

    gfposc_c(std::to_string(body.GetId()).c_str(), m_frame->GetName().c_str(), abe.ToString(aberrationCorrection).c_str(), std::to_string(m_id).c_str(),
             IO::SDK::CoordinateSystem::Latitudinal().ToCharArray(), IO::SDK::Coordinate::Latitude().ToCharArray(), ">", 0.0, 0.0, 60.0, NINTVL, &cnfine, &results);

    for (int i = 0; i < wncard_c(&results); i++)
    {
        wnfetd_c(&results, i, &windowStart, &windowEnd);
        windows.push_back(IO::SDK::Time::Window<IO::SDK::Time::UTC>(
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowStart)).ToUTC(),
                IO::SDK::Time::TDB(std::chrono::duration<double>(windowEnd)).ToUTC()));
    }
    return windows;
}

void IO::SDK::Sites::Site::WriteEphemeris(const std::vector<OrbitalParameters::StateVector> &states) const
{
    return this->m_ephemerisKernel->WriteData(states);
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Sites::Site::ReadEphemeris(const IO::SDK::Frames::Frames &frame,
                                                                            const IO::SDK::AberrationsEnum aberration,
                                                                            const IO::SDK::Time::TDB &epoch,
                                                                            const IO::SDK::Body::CelestialBody &observer) const
{
    return this->m_ephemerisKernel->ReadStateVector(observer, frame, aberration, epoch);
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> IO::SDK::Sites::Site::GetEphemerisCoverageWindow() const
{
    return this->m_ephemerisKernel->GetCoverageWindow();
}

void IO::SDK::Sites::Site::WriteEphemerisKernelComment(const std::string &comment) const
{
    this->m_ephemerisKernel->AddComment(comment);
}

std::string IO::SDK::Sites::Site::ReadEphemerisKernelComment() const
{
    return this->m_ephemerisKernel->ReadComment();
}
