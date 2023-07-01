/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <StateVector.h>
#include <InertialFrames.h>
#include <StringHelpers.h>
#include <Type.h>
#include <Constants.h>

using namespace std::chrono_literals;

IO::Astrodynamics::Body::Body::Body(const int id, const std::string &name, const double mass)
        : m_id{id},
          m_name{IO::Astrodynamics::StringHelpers::ToUpper(name)},
          m_mass{mass > 0 ? mass : throw IO::Astrodynamics::Exception::SDKException("Mass must be a positive value")},
          m_mu{mass * IO::Astrodynamics::Constants::G}
{
}

IO::Astrodynamics::Body::Body::Body(const int id, const std::string &name, const double mass, std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch) : Body(
        id, name, mass)
{
    m_orbitalParametersAtEpoch = std::move(orbitalParametersAtEpoch);
    m_orbitalParametersAtEpoch->GetCenterOfMotion()->m_satellites.push_back(this);
}

IO::Astrodynamics::Body::Body::Body(const int id, const std::string &name, const double mass, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion) : Body(id, name, mass)
{
    m_orbitalParametersAtEpoch = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            this->ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::GetICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB(0s), *centerOfMotion));
    centerOfMotion->m_satellites.push_back(this);
}

IO::Astrodynamics::Body::Body::Body(const Body &body) : Body(body.m_id, body.m_name, body.m_mass)
{}

int IO::Astrodynamics::Body::Body::GetId() const
{
    return m_id;
}

std::string IO::Astrodynamics::Body::Body::GetName() const
{
    return m_name;
}

double IO::Astrodynamics::Body::Body::GetMass() const
{
    return m_mass;
}

double IO::Astrodynamics::Body::Body::GetMu() const
{
    return m_mu;
}

const std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> &IO::Astrodynamics::Body::Body::GetOrbitalParametersAtEpoch() const
{
    return m_orbitalParametersAtEpoch;
}

const std::vector<IO::Astrodynamics::Body::Body *> &IO::Astrodynamics::Body::Body::GetSatellites() const
{
    return m_satellites;
}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Body::Body::ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame, const IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TDB &epoch,
                                   const IO::Astrodynamics::Body::CelestialBody &relativeTo) const
{
    SpiceDouble vs[6];
    SpiceDouble lt;
    spkezr_c(std::to_string(m_id).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), IO::Astrodynamics::Aberrations::ToString(aberration).c_str(),
             std::to_string(relativeTo.m_id).c_str(), vs, &lt);

    //Convert to SDK unit
    for (double &v: vs)
    {
        v = v * 1000.0; /* code */
    }

    return IO::Astrodynamics::OrbitalParameters::StateVector{std::make_shared<IO::Astrodynamics::Body::CelestialBody>(relativeTo), vs, epoch, frame};
}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Body::Body::ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame, const IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TDB &epoch) const
{
    SpiceDouble vs[6];
    SpiceDouble lt;
    spkezr_c(std::to_string(m_id).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), IO::Astrodynamics::Aberrations::ToString(aberration).c_str(),
             std::to_string(m_orbitalParametersAtEpoch->GetCenterOfMotion()->m_id).c_str(), vs, &lt);
    //Convert to SDK unit
    for (double &v: vs)
    {
        v = v * 1000.0; /* code */
    }
    return IO::Astrodynamics::OrbitalParameters::StateVector{m_orbitalParametersAtEpoch->GetCenterOfMotion(), vs, epoch, frame};
}

bool IO::Astrodynamics::Body::Body::operator==(const IO::Astrodynamics::Body::Body &rhs) const
{
    return m_id == rhs.m_id;
}

bool IO::Astrodynamics::Body::Body::operator!=(const IO::Astrodynamics::Body::Body &rhs) const
{
    return m_id != rhs.m_id;
}

std::shared_ptr<IO::Astrodynamics::Body::Body> IO::Astrodynamics::Body::Body::GetSharedPointer()
{
    return this->shared_from_this();
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Body::Body::FindWindowsOnDistanceConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &window, const Body &targetBody, const Body &observer,
                                                     const IO::Astrodynamics::Constraints::RelationalOperator &constraint, const IO::Astrodynamics::AberrationsEnum aberration, const double value,
                                                     const IO::Astrodynamics::Time::TimeSpan &step)
{
    return IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(window, observer.m_id, targetBody.m_id, constraint, value, aberration, step);
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Body::Body::FindWindowsOnOccultationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, const IO::Astrodynamics::Body::Body &targetBody,
                                                        const IO::Astrodynamics::Body::CelestialBody &frontBody, const IO::Astrodynamics::OccultationType &occultationType,
                                                        const IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TimeSpan &stepSize) const
{
    std::string bshape{"POINT"};
    std::string bframe{};
    auto selectedOccultation {OccultationType::Any()};
    if (IO::Astrodynamics::Helpers::IsInstanceOf<IO::Astrodynamics::Body::CelestialBody>(&targetBody))
    {
        bshape = "ELLIPSOID";
        bframe = dynamic_cast<const IO::Astrodynamics::Body::CelestialBody &>(targetBody).GetBodyFixedFrame().GetName();
        selectedOccultation = occultationType;
    }
    return IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnOccultationConstraint(searchWindow, m_id, targetBody.m_id, bframe, bshape, frontBody.m_id,
                                                                                    frontBody.GetBodyFixedFrame().GetName(), "ELLIPSOID", selectedOccultation, aberration,
                                                                                    stepSize);
}

IO::Astrodynamics::Coordinates::Planetographic
IO::Astrodynamics::Body::Body::GetSubObserverPoint(const IO::Astrodynamics::Body::CelestialBody &targetBody, const IO::Astrodynamics::AberrationsEnum &aberration, const IO::Astrodynamics::Time::DateTime &epoch) const
{
    SpiceDouble spoint[3];
    SpiceDouble srfVector[3];
    SpiceDouble subEpoch;
    subpnt_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(),
             IO::Astrodynamics::Aberrations::ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
    SpiceDouble lat, lon, alt;
    recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX(), targetBody.GetFlattening(), &lon, &lat, &alt);

    return IO::Astrodynamics::Coordinates::Planetographic{lon, lat, alt};
}

IO::Astrodynamics::Coordinates::Planetographic
IO::Astrodynamics::Body::Body::GetSubSolarPoint(const IO::Astrodynamics::Body::CelestialBody &targetBody, const IO::Astrodynamics::AberrationsEnum aberration, const IO::Astrodynamics::Time::TDB &epoch) const
{
    SpiceDouble spoint[3];
    SpiceDouble srfVector[3];
    SpiceDouble subEpoch;
    subslr_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(),
             IO::Astrodynamics::Aberrations::ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
    SpiceDouble lat, lon, alt;
    recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX(), targetBody.GetFlattening(), &lon, &lat, &alt);

    return IO::Astrodynamics::Coordinates::Planetographic{lon, lat, alt};
}