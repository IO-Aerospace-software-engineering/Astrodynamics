/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <Site.h>
#include <Parameters.h>
#include <Constants.h>

#include <InertialFrames.h>
#include <algorithm>

using namespace std::chrono_literals;

IO::Astrodynamics::Sites::Site::Site(const int id, std::string name, const IO::Astrodynamics::Coordinates::Planetodetic &coordinates,
                           std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> body, std::string directoryPath) : m_id{id},
                                                                                                            m_name{std::move(name)},
                                                                                                            m_coordinates{coordinates},
                                                                                                            m_filesPath{std::move(directoryPath) + "/" + m_name},
                                                                                                            m_ephemerisKernel{std::make_unique<IO::Astrodynamics::Kernels::EphemerisKernel>(
                                                                                                                    m_filesPath + "/Ephemeris/" + m_name + ".spk", this->m_id)},
                                                                                                            m_body{std::move(body)},
                                                                                                            m_frame{std::make_unique<IO::Astrodynamics::Frames::SiteFrameFile>(*this)}
{
    if (id < 199000 || id > 899999)
    {
        throw IO::Astrodynamics::Exception::SDKException(
                "Invalid site id. Site id must be composed by the site body id and the site number. Ex. The site 232 on earth (399) must have the id 399232.");
    }
}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Sites::Site::GetStateVector(const IO::Astrodynamics::Frames::Frames &frame, const IO::Astrodynamics::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius() * 1000.0;
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(), radius.GetX(),
             m_body->GetFlattening(), bodyFixedLocation);
    IO::Astrodynamics::OrbitalParameters::StateVector siteVectorState{m_body, IO::Astrodynamics::Math::Vector3D(bodyFixedLocation[0],
                                                                                            bodyFixedLocation[1],
                                                                                            bodyFixedLocation[2]),
                                                            IO::Astrodynamics::Math::Vector3D(), epoch,
                                                            m_body->GetBodyFixedFrame()};

    return siteVectorState.ToFrame(frame);
}

IO::Astrodynamics::Coordinates::Equatorial
IO::Astrodynamics::Sites::Site::GetRADec(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::AberrationsEnum aberrationCorrection,
                               const IO::Astrodynamics::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius();
    auto bodiesSv = body.ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), aberrationCorrection, epoch,
                                       *m_body);

    auto siteVector = GetStateVector(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), epoch);

    auto resultSv = bodiesSv.GetPosition() - siteVector.GetPosition();

    ConstSpiceDouble rectan[3]{resultSv.GetX(), resultSv.GetY(), resultSv.GetZ()};
    double r, ra, dec;
    recrad_c(rectan, &r, &ra, &dec);

    return IO::Astrodynamics::Coordinates::Equatorial{ra, dec, r};
}

IO::Astrodynamics::Illumination::Illumination
IO::Astrodynamics::Sites::Site::GetIllumination(const IO::Astrodynamics::AberrationsEnum aberrationCorrection,
                                      const IO::Astrodynamics::Time::TDB &epoch) const
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

    return IO::Astrodynamics::Illumination::Illumination{
            IO::Astrodynamics::Math::Vector3D(srfvec[0] * 1000.0, srfvec[1] * 1000.0, srfvec[2] * 1000.0), pha, inc, emi,
            IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(srfEpoch))};
}

bool IO::Astrodynamics::Sites::Site::IsDay(const IO::Astrodynamics::Time::TDB &epoch, const double twilight) const
{
    return GetIllumination(AberrationsEnum::CNS, epoch).GetIncidence() < IO::Astrodynamics::Constants::PI2 - twilight;
}

bool IO::Astrodynamics::Sites::Site::IsNight(const IO::Astrodynamics::Time::TDB &epoch, const double twilight) const
{
    return GetIllumination(AberrationsEnum::CNS, epoch).GetIncidence() >= IO::Astrodynamics::Constants::PI2 - twilight;
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
IO::Astrodynamics::Sites::Site::FindDayWindows(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow,
                                     const double twilight) const
{
    IO::Astrodynamics::Body::CelestialBody sun(10);
    return FindWindowsOnIlluminationConstraint(searchWindow, sun, IO::Astrodynamics::IlluminationAngle::Incidence(),
                                               IO::Astrodynamics::Constraints::RelationalOperator::LowerThan(), IO::Astrodynamics::Constants::PI2 - twilight);
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
IO::Astrodynamics::Sites::Site::FindNightWindows(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow,
                                       const double twilight) const
{
    IO::Astrodynamics::Body::CelestialBody sun(10);
    return FindWindowsOnIlluminationConstraint(searchWindow, sun, IO::Astrodynamics::IlluminationAngle::Incidence(),
                                               IO::Astrodynamics::Constraints::RelationalOperator::GreaterThan(), IO::Astrodynamics::Constants::PI2 - twilight);
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
IO::Astrodynamics::Sites::Site::FindWindowsOnIlluminationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow,
                                                          const IO::Astrodynamics::Body::Body &observerBody,
                                                          const IO::Astrodynamics::IlluminationAngle &illuminationAngle,
                                                          const IO::Astrodynamics::Constraints::RelationalOperator &constraint,
                                                          const double value) const
{
    IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> tdbWindow(searchWindow.GetStartDate().ToTDB(), searchWindow.GetEndDate().ToTDB());
    std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>> windows;
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(),
             m_body->GetRadius().GetX(), m_body->GetFlattening(), bodyFixedLocation);


    auto res= IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnIlluminationConstraint(tdbWindow, observerBody.GetId(), "Sun", m_body->GetId(), m_body->GetBodyFixedFrame().GetName(),
                                                                              bodyFixedLocation, illuminationAngle, constraint, value, 0.0,
                                                                              IO::Astrodynamics::AberrationsEnum::CNS, IO::Astrodynamics::Time::TimeSpan(std::chrono::duration<double>(4.5 * 60 * 60)),
                                                                              "Ellipsoid");

    std::vector<IO::Astrodynamics::Time::Window<Time::UTC>> utcWindows;
    utcWindows.reserve(res.size());

    std::for_each(res.begin(), res.end(), [&utcWindows](Time::Window<Time::TDB> &x) { utcWindows.emplace_back(x.GetStartDate().ToUTC(), x.GetEndDate().ToUTC()); });

    return utcWindows;


}

IO::Astrodynamics::Coordinates::HorizontalCoordinates IO::Astrodynamics::Sites::Site::GetHorizontalCoordinates(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::AberrationsEnum aberrationCorrection,
                                                                                           const IO::Astrodynamics::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius();
    SpiceDouble bodyFixedLocation[3];
    georec_c(m_coordinates.GetLongitude(), m_coordinates.GetLatitude(), m_coordinates.GetAltitude(), radius.GetX(),
             m_body->GetFlattening(), bodyFixedLocation);

    SpiceDouble res[6];
    SpiceDouble lt;
    azlcpo_c("ELLIPSOID", std::to_string(body.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(),
             IO::Astrodynamics::Aberrations::ToString(aberrationCorrection).c_str(), false, true, bodyFixedLocation,
             std::to_string(m_body->GetId()).c_str(),
             m_body->GetBodyFixedFrame().GetName().c_str(), res, &lt);

    return IO::Astrodynamics::Coordinates::HorizontalCoordinates{res[1], res[2], res[0] * 1000.0};
}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Sites::Site::GetStateVector(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::Frames::Frames &frame,
                                     IO::Astrodynamics::AberrationsEnum aberrationCorrection,
                                     const IO::Astrodynamics::Time::TDB &epoch) const
{
    auto radius = m_body->GetRadius();
    auto bodiesSv = body.ReadEphemeris(frame, aberrationCorrection, epoch, *m_body);

    auto siteVector = GetStateVector(frame, epoch);

    return IO::Astrodynamics::OrbitalParameters::StateVector{m_body, bodiesSv.GetPosition() - siteVector.GetPosition(),
                                                   bodiesSv.GetVelocity() - siteVector.GetVelocity(), epoch, frame};
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
IO::Astrodynamics::Sites::Site::FindBodyVisibilityWindows(const IO::Astrodynamics::Body::Body &body,
                                                const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow,
                                                const IO::Astrodynamics::AberrationsEnum aberrationCorrection) const
{
    IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> tdbWindow(searchWindow.GetStartDate().ToTDB(), searchWindow.GetEndDate().ToTDB());

    auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnCoordinateConstraint(tdbWindow, m_id, body.GetId(), m_frame->GetName(),
                                                                                       IO::Astrodynamics::CoordinateSystem::Latitudinal(), IO::Astrodynamics::Coordinate::Latitude(),
                                                                                       Constraints::RelationalOperator::GreaterThan(), 0.0, 0.0, aberrationCorrection,
                                                                                       IO::Astrodynamics::Time::TimeSpan(std::chrono::duration<double>(60.0)));
    std::vector<IO::Astrodynamics::Time::Window<Time::UTC>> utcWindows;
    utcWindows.reserve(res.size());

    std::for_each(res.begin(), res.end(), [&utcWindows](Time::Window<Time::TDB> &x) { utcWindows.emplace_back(x.GetStartDate().ToUTC(), x.GetEndDate().ToUTC()); });

    return utcWindows;
}

void IO::Astrodynamics::Sites::Site::WriteEphemeris(const std::vector<OrbitalParameters::StateVector> &states) const
{
    return this->m_ephemerisKernel->WriteData(states);
}

IO::Astrodynamics::OrbitalParameters::StateVector IO::Astrodynamics::Sites::Site::ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame,
                                                                            const IO::Astrodynamics::AberrationsEnum aberration,
                                                                            const IO::Astrodynamics::Time::TDB &epoch,
                                                                            const IO::Astrodynamics::Body::CelestialBody &observer) const
{
    return this->m_ephemerisKernel->ReadStateVector(observer, frame, aberration, epoch);
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> IO::Astrodynamics::Sites::Site::GetEphemerisCoverageWindow() const
{
    return this->m_ephemerisKernel->GetCoverageWindow();
}

void IO::Astrodynamics::Sites::Site::WriteEphemerisKernelComment(const std::string &comment) const
{
    this->m_ephemerisKernel->AddComment(comment);
}

std::string IO::Astrodynamics::Sites::Site::ReadEphemerisKernelComment() const
{
    return this->m_ephemerisKernel->ReadComment();
}

void IO::Astrodynamics::Sites::Site::BuildAndWriteEphemeris(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &window) const
{
    std::vector<IO::Astrodynamics::OrbitalParameters::StateVector> svector;
    for (auto epoch = window.GetStartDate(); epoch <= window.GetEndDate(); epoch = epoch + IO::Astrodynamics::Parameters::SitePropagationStep)
    {
        auto sv = GetStateVector(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), epoch);
        svector.push_back(sv);
    }

    //Add latest value
    if (svector.back().GetEpoch() < window.GetEndDate())
    {
        auto sv = GetStateVector(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), window.GetEndDate());
        svector.push_back(sv);
    }

    WriteEphemeris(svector);
}
