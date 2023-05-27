/**
 * @file Body.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <StateVector.h>
#include <InertialFrames.h>
#include <Builder.h>
#include <StringHelpers.h>
#include "Helpers/Type.cpp"
#include <Constants.h>
#include <GeometryFinder.h>

using namespace std::chrono_literals;

IO::SDK::Body::Body::Body(const int id, const std::string &name, const double mass)
        : m_id{id},
          m_name{IO::SDK::StringHelpers::ToUpper(name)},
          m_mass{mass > 0 ? mass : throw IO::SDK::Exception::SDKException("Mass must be a positive value")},
          m_mu{mass * IO::SDK::Constants::G}
{
}

IO::SDK::Body::Body::Body(const int id, const std::string &name, const double mass, std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch) : Body(
        id, name, mass)
{
    m_orbitalParametersAtEpoch = std::move(orbitalParametersAtEpoch);
    m_orbitalParametersAtEpoch->GetCenterOfMotion()->m_satellites.push_back(this);
}

IO::SDK::Body::Body::Body(const int id, const std::string &name, const double mass, std::shared_ptr<IO::SDK::Body::CelestialBody> &centerOfMotion) : Body(id, name, mass)
{
    m_orbitalParametersAtEpoch = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            this->ReadEphemeris(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::AberrationsEnum::None, IO::SDK::Time::TDB(0s), *centerOfMotion));
    centerOfMotion->m_satellites.push_back(this);
}

IO::SDK::Body::Body::Body(const Body &body) : Body(body.m_id, body.m_name, body.m_mass)
{}

int IO::SDK::Body::Body::GetId() const
{
    return m_id;
}

std::string IO::SDK::Body::Body::GetName() const
{
    return m_name;
}

double IO::SDK::Body::Body::GetMass() const
{
    return m_mass;
}

double IO::SDK::Body::Body::GetMu() const
{
    return m_mu;
}

const std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> &IO::SDK::Body::Body::GetOrbitalParametersAtEpoch() const
{
    return m_orbitalParametersAtEpoch;
}

const std::vector<IO::SDK::Body::Body *> &IO::SDK::Body::Body::GetSatellites() const
{
    return m_satellites;
}

IO::SDK::OrbitalParameters::StateVector
IO::SDK::Body::Body::ReadEphemeris(const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch,
                                   const IO::SDK::Body::CelestialBody &relativeTo) const
{
    SpiceDouble vs[6];
    SpiceDouble lt;
    spkezr_c(std::to_string(m_id).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), IO::SDK::Aberrations::ToString(aberration).c_str(),
             std::to_string(relativeTo.m_id).c_str(), vs, &lt);

    //Convert to SDK unit
    for (double &v: vs)
    {
        v = v * 1000.0; /* code */
    }

    return IO::SDK::OrbitalParameters::StateVector{std::make_shared<IO::SDK::Body::CelestialBody>(relativeTo), vs, epoch, frame};
}

IO::SDK::OrbitalParameters::StateVector
IO::SDK::Body::Body::ReadEphemeris(const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const
{
    SpiceDouble vs[6];
    SpiceDouble lt;
    spkezr_c(std::to_string(m_id).c_str(), epoch.GetSecondsFromJ2000().count(), frame.ToCharArray(), IO::SDK::Aberrations::ToString(aberration).c_str(),
             std::to_string(m_orbitalParametersAtEpoch->GetCenterOfMotion()->m_id).c_str(), vs, &lt);
    //Convert to SDK unit
    for (double &v: vs)
    {
        v = v * 1000.0; /* code */
    }
    return IO::SDK::OrbitalParameters::StateVector{m_orbitalParametersAtEpoch->GetCenterOfMotion(), vs, epoch, frame};
}

bool IO::SDK::Body::Body::operator==(const IO::SDK::Body::Body &rhs) const
{
    return m_id == rhs.m_id;
}

bool IO::SDK::Body::Body::operator!=(const IO::SDK::Body::Body &rhs) const
{
    return m_id != rhs.m_id;
}

std::shared_ptr<IO::SDK::Body::Body> IO::SDK::Body::Body::GetSharedPointer()
{
    return this->shared_from_this();
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Body::Body::FindWindowsOnDistanceConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &window, const Body &targetBody, const Body &observer,
                                                     const IO::SDK::Constraints::RelationalOperator &constraint, const IO::SDK::AberrationsEnum aberration, const double value,
                                                     const IO::SDK::Time::TimeSpan &step)
{
    return IO::SDK::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(window, observer.m_id, targetBody.m_id, constraint, value, aberration, step);
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Body::Body::FindWindowsOnOccultationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const IO::SDK::Body::Body &targetBody,
                                                        const IO::SDK::Body::CelestialBody &frontBody, const IO::SDK::OccultationType &occultationType,
                                                        const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TimeSpan &stepSize) const
{
    std::string bshape{"POINT"};
    std::string bframe{};
    auto selectedOccultation {OccultationType::Any()};
    if (IO::SDK::Helpers::IsInstanceOf<IO::SDK::Body::CelestialBody>(&targetBody))
    {
        bshape = "ELLIPSOID";
        bframe = dynamic_cast<const IO::SDK::Body::CelestialBody &>(targetBody).GetBodyFixedFrame().GetName();
        selectedOccultation = occultationType;
    }
    return IO::SDK::Constraints::GeometryFinder::FindWindowsOnOccultationConstraint(searchWindow, m_id, targetBody.m_id, bframe, bshape, frontBody.m_id,
                                                                                    frontBody.GetBodyFixedFrame().GetName(), "ELLIPSOID", selectedOccultation, aberration,
                                                                                    stepSize);
}

IO::SDK::Coordinates::Planetographic
IO::SDK::Body::Body::GetSubObserverPoint(const IO::SDK::Body::CelestialBody &targetBody, const IO::SDK::AberrationsEnum &aberration, const IO::SDK::Time::DateTime &epoch) const
{
    SpiceDouble spoint[3];
    SpiceDouble srfVector[3];
    SpiceDouble subEpoch;
    subpnt_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(),
             IO::SDK::Aberrations::ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
    SpiceDouble lat, lon, alt;
    recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX(), targetBody.GetFlattening(), &lon, &lat, &alt);

    return IO::SDK::Coordinates::Planetographic{lon, lat, alt};
}

IO::SDK::Coordinates::Planetographic
IO::SDK::Body::Body::GetSubSolarPoint(const IO::SDK::Body::CelestialBody &targetBody, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const
{
    SpiceDouble spoint[3];
    SpiceDouble srfVector[3];
    SpiceDouble subEpoch;
    subslr_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(),
             IO::SDK::Aberrations::ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
    SpiceDouble lat, lon, alt;
    recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX(), targetBody.GetFlattening(), &lon, &lat, &alt);

    return IO::SDK::Coordinates::Planetographic{lon, lat, alt};
}