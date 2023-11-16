/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <StateVector.h>
#include <InertialFrames.h>
#include <StringHelpers.h>
#include <Type.h>
#include <Constants.h>

using namespace std::chrono_literals;

IO::Astrodynamics::Body::CelestialItem::CelestialItem(const int id, const std::string &name, const double mass)
        : m_id{id},
          m_name{IO::Astrodynamics::StringHelpers::ToUpper(name)},
          m_mass{mass >= 0 ? mass : throw IO::Astrodynamics::Exception::SDKException("Mass must be a positive value")},
          m_mu{mass * IO::Astrodynamics::Constants::G}
{
}

IO::Astrodynamics::Body::CelestialItem::CelestialItem(const int id, const std::string &name, const double mass,
                                                      std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch) : CelestialItem(
        id, name, mass)
{
    m_orbitalParametersAtEpoch = std::move(orbitalParametersAtEpoch);
    m_orbitalParametersAtEpoch->GetCenterOfMotion()->m_satellites.push_back(this);
}

IO::Astrodynamics::Body::CelestialItem::CelestialItem(const int id, const std::string &name, const double mass,
                                                      std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &centerOfMotion) : CelestialItem(id, name, mass)
{
    m_orbitalParametersAtEpoch = std::make_unique<IO::Astrodynamics::OrbitalParameters::StateVector>(
            this->ReadEphemeris(IO::Astrodynamics::Frames::InertialFrames::ICRF(), IO::Astrodynamics::AberrationsEnum::None, IO::Astrodynamics::Time::TDB(0s), *centerOfMotion));
    centerOfMotion->m_satellites.push_back(this);
}

IO::Astrodynamics::Body::CelestialItem::CelestialItem(const CelestialItem &body) : CelestialItem(body.m_id, body.m_name, body.m_mass)
{}

int IO::Astrodynamics::Body::CelestialItem::GetId() const
{
    return m_id;
}

std::string IO::Astrodynamics::Body::CelestialItem::GetName() const
{
    return m_name;
}

double IO::Astrodynamics::Body::CelestialItem::GetMass() const
{
    return m_mass;
}

double IO::Astrodynamics::Body::CelestialItem::GetMu() const
{
    return m_mu;
}

const std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> &IO::Astrodynamics::Body::CelestialItem::GetOrbitalParametersAtEpoch() const
{
    return m_orbitalParametersAtEpoch;
}

const std::vector<IO::Astrodynamics::Body::CelestialItem *> &IO::Astrodynamics::Body::CelestialItem::GetSatellites() const
{
    return m_satellites;
}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Body::CelestialItem::ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame, const IO::Astrodynamics::AberrationsEnum aberration,
                                                      const IO::Astrodynamics::Time::TDB &epoch,
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
IO::Astrodynamics::Body::CelestialItem::ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame, const IO::Astrodynamics::AberrationsEnum aberration,
                                                      const IO::Astrodynamics::Time::TDB &epoch) const
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

bool IO::Astrodynamics::Body::CelestialItem::operator==(const IO::Astrodynamics::Body::CelestialItem &rhs) const
{
    return m_id == rhs.m_id;
}

bool IO::Astrodynamics::Body::CelestialItem::operator!=(const IO::Astrodynamics::Body::CelestialItem &rhs) const
{
    return m_id != rhs.m_id;
}

std::shared_ptr<IO::Astrodynamics::Body::CelestialItem> IO::Astrodynamics::Body::CelestialItem::GetSharedPointer()
{
    return this->shared_from_this();
}

std::shared_ptr<IO::Astrodynamics::Body::CelestialItem> IO::Astrodynamics::Body::CelestialItem::GetSharedPointer() const
{
    return std::const_pointer_cast<IO::Astrodynamics::Body::CelestialItem>(this->shared_from_this());
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Body::CelestialItem::FindWindowsOnDistanceConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &window,
                                                                        const CelestialItem &targetBody, const CelestialItem &observer,
                                                                        const IO::Astrodynamics::Constraints::RelationalOperator &constraint,
                                                                        const IO::Astrodynamics::AberrationsEnum aberration, const double value,
                                                                        const IO::Astrodynamics::Time::TimeSpan &step)
{
    return IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(window, observer.m_id, targetBody.m_id, constraint, value, aberration, step);
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Body::CelestialItem::FindWindowsOnOccultationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow,
                                                                           const IO::Astrodynamics::Body::CelestialItem &targetBody,
                                                                           const IO::Astrodynamics::Body::CelestialBody &frontBody,
                                                                           const IO::Astrodynamics::OccultationType &occultationType,
                                                                           const IO::Astrodynamics::AberrationsEnum aberration,
                                                                           const IO::Astrodynamics::Time::TimeSpan &stepSize) const
{
    std::string bshape{"POINT"};
    std::string bframe{};
    auto selectedOccultation{OccultationType::Any()};
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
IO::Astrodynamics::Body::CelestialItem::GetSubObserverPoint(const IO::Astrodynamics::Body::CelestialBody &targetBody, const IO::Astrodynamics::AberrationsEnum &aberration,
                                                            const IO::Astrodynamics::Time::DateTime &epoch) const
{
    SpiceDouble spoint[3];
    SpiceDouble srfVector[3];
    SpiceDouble subEpoch;
    subpnt_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(),
             IO::Astrodynamics::Aberrations::ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
    SpiceDouble lat, lon, alt;
    recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX() * 0.001, targetBody.GetFlattening(), &lon, &lat, &alt);

    return IO::Astrodynamics::Coordinates::Planetographic{lon, lat, alt};
}

IO::Astrodynamics::Coordinates::Planetographic
IO::Astrodynamics::Body::CelestialItem::GetSubSolarPoint(const IO::Astrodynamics::Body::CelestialBody &targetBody, const IO::Astrodynamics::AberrationsEnum aberration,
                                                         const IO::Astrodynamics::Time::TDB &epoch) const
{
    SpiceDouble spoint[3];
    SpiceDouble srfVector[3];
    SpiceDouble subEpoch;
    subslr_c("INTERCEPT/ELLIPSOID", std::to_string(targetBody.GetId()).c_str(), epoch.GetSecondsFromJ2000().count(), targetBody.GetBodyFixedFrame().GetName().c_str(),
             IO::Astrodynamics::Aberrations::ToString(aberration).c_str(), std::to_string(m_id).c_str(), spoint, &subEpoch, srfVector);
    SpiceDouble lat, lon, alt;
    recpgr_c(std::to_string(targetBody.GetId()).c_str(), spoint, targetBody.GetRadius().GetX() * 0.001, targetBody.GetFlattening(), &lon, &lat, &alt);

    return IO::Astrodynamics::Coordinates::Planetographic{lon, lat, alt};
}


