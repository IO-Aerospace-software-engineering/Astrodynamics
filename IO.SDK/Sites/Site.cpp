/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <Site.h>
#include <Parameters.h>
#include <Constants.h>

#include <InertialFrames.h>
#include <algorithm>

using namespace std::chrono_literals;

IO::SDK::Sites::Site::Site(const int id, std::string name, const IO::SDK::Coordinates::Geodetic &coordinates,
                           std::shared_ptr<IO::SDK::Body::CelestialBody> body, std::string directoryPath) : m_id{id},
                                                                                                            m_name{std::move(name)},
                                                                                                            m_coordinates{coordinates},
                                                                                                            m_filesPath{std::move(directoryPath) + "/" + m_name},
                                                                                                            m_ephemerisKernel{std::make_unique<IO::SDK::Kernels::EphemerisKernel>(
                                                                                                                    m_filesPath + "/Ephemeris/" + m_name + ".spk", this->m_id)},
                                                                                                            m_body{std::move(body)},
                                                                                                            m_frame{std::make_unique<IO::SDK::Frames::SiteFrameFile>(*this)}
{
    if (id < 199000 || id > 899999)
    {
        throw SDK::Exception::SDKException(
                "Invalid site id. Site id must be composed by the site body id and the site number. Ex. The site 232 on earth (399) must have the id 399232.");
    }
}

IO::SDK::OrbitalParameters::StateVector
IO::SDK::Sites::Site::GetStateVector(const IO::SDK::Frames::Frames &frame, const IO::SDK::Time::TDB &epoch) const
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

IO::SDK::Coordinates::Equatorial
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

    return IO::SDK::Coordinates::Equatorial{ra, dec, r};
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

    return IO::SDK::Illumination::Illumination{
            IO::SDK::Math::Vector3D(srfvec[0] * 1000.0, srfvec[1] * 1000.0, srfvec[2] * 1000.0), pha, inc, emi,
            IO::SDK::Time::TDB(std::chrono::duration<double>(srfEpoch))};
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
    IO::SDK::Body::CelestialBody sun(10);
    return FindWindowsOnIlluminationConstraint(searchWindow, sun, IO::SDK::IlluminationAngle::Incidence(),
                                               IO::SDK::Constraints::RelationalOperator::LowerThan(), IO::SDK::Constants::PI2 - twilight);
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
IO::SDK::Sites::Site::FindNightWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                       const double twilight) const
{
    IO::SDK::Body::CelestialBody sun(10);
    return FindWindowsOnIlluminationConstraint(searchWindow, sun, IO::SDK::IlluminationAngle::Incidence(),
                                               IO::SDK::Constraints::RelationalOperator::GreaterThan(), IO::SDK::Constants::PI2 - twilight);
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
IO::SDK::Sites::Site::FindWindowsOnIlluminationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                                          const IO::SDK::Body::Body &observerBody,
                                                          const IO::SDK::IlluminationAngle &illuminationAngle,
                                                          const IO::SDK::Constraints::RelationalOperator &constraint,
                                                          const double value) const
{
    IO::SDK::Time::Window<IO::SDK::Time::TDB> tdbWindow(searchWindow.GetStartDate().ToTDB(), searchWindow.GetEndDate().ToTDB());
    std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> windows;
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(),
             m_body->GetRadius().GetX(), m_body->GetFlattening(), bodyFixedLocation);


    auto res= IO::SDK::Constraints::GeometryFinder::FindWindowsOnIlluminationConstraint(tdbWindow, observerBody.GetId(), "Sun", m_body->GetId(), m_body->GetBodyFixedFrame().GetName(),
                                                                              bodyFixedLocation, illuminationAngle, constraint, value, 0.0,
                                                                              IO::SDK::AberrationsEnum::CNS, IO::SDK::Time::TimeSpan(std::chrono::duration<double>(4.5 * 60 * 60)),
                                                                              "Ellipsoid");

    std::vector<IO::SDK::Time::Window<Time::UTC>> utcWindows;
    utcWindows.reserve(res.size());

    std::for_each(res.begin(), res.end(), [&utcWindows](Time::Window<Time::TDB> &x) { utcWindows.emplace_back(x.GetStartDate().ToUTC(), x.GetEndDate().ToUTC()); });

    return utcWindows;


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
             IO::SDK::Aberrations::ToString(aberrationCorrection).c_str(), false, true, bodyFixedLocation,
             std::to_string(m_body->GetId()).c_str(),
             m_body->GetBodyFixedFrame().GetName().c_str(), res, &lt);

    return IO::SDK::Coordinates::HorizontalCoordinates{res[1], res[2], res[0] * 1000.0};
}

IO::SDK::OrbitalParameters::StateVector
IO::SDK::Sites::Site::GetStateVector(const IO::SDK::Body::Body &body, const IO::SDK::Frames::Frames &frame,
                                     IO::SDK::AberrationsEnum aberrationCorrection,
                                     const IO::SDK::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius();
    auto bodiesSv = body.ReadEphemeris(frame, aberrationCorrection, epoch, *m_body);

    auto siteVector = GetStateVector(frame, epoch);

    return IO::SDK::OrbitalParameters::StateVector{m_body, bodiesSv.GetPosition() - siteVector.GetPosition(),
                                                   bodiesSv.GetVelocity() - siteVector.GetVelocity(), epoch, frame};
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
IO::SDK::Sites::Site::FindBodyVisibilityWindows(const IO::SDK::Body::Body &body,
                                                const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                                const IO::SDK::AberrationsEnum aberrationCorrection) const
{
    IO::SDK::Time::Window<IO::SDK::Time::TDB> tdbWindow(searchWindow.GetStartDate().ToTDB(), searchWindow.GetEndDate().ToTDB());

    auto res = IO::SDK::Constraints::GeometryFinder::FindWindowsOnCoordinateConstraint(tdbWindow, m_id, body.GetId(), m_frame->GetName(),
                                                                                       IO::SDK::CoordinateSystem::Latitudinal(), IO::SDK::Coordinate::Latitude(),
                                                                                       Constraints::RelationalOperator::GreaterThan(), 0.0, 0.0, aberrationCorrection,
                                                                                       IO::SDK::Time::TimeSpan(std::chrono::duration<double>(60.0)));
    std::vector<IO::SDK::Time::Window<Time::UTC>> utcWindows;
    utcWindows.reserve(res.size());

    std::for_each(res.begin(), res.end(), [&utcWindows](Time::Window<Time::TDB> &x) { utcWindows.emplace_back(x.GetStartDate().ToUTC(), x.GetEndDate().ToUTC()); });

    return utcWindows;
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

void IO::SDK::Sites::Site::BuildAndWriteEphemeris(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow) const
{
    std::vector<IO::SDK::OrbitalParameters::StateVector> svector;
    for (auto epoch = searchWindow.GetStartDate().ToTDB(); epoch <= searchWindow.GetEndDate().ToTDB(); epoch = epoch + IO::SDK::Parameters::SitePropagationStep)
    {
        auto sv = GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), epoch);
        svector.push_back(sv);
    }

    //Add latest value
    if (svector.back().GetEpoch() < searchWindow.GetEndDate().ToTDB())
    {
        auto sv = GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), searchWindow.GetEndDate().ToTDB());
        svector.push_back(sv);
    }

    WriteEphemeris(svector);
}
