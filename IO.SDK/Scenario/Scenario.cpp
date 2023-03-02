//
// Created by s.guillet on 23/02/2023.
//

#include <Scenario.h>

IO::SDK::Scenario::Scenario(const std::string &name, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windows) : m_name{name}, m_windows{windows}
{

}

void IO::SDK::Scenario::AddSite(const IO::SDK::Sites::Site &site)
{
    m_sites.push_back(&site);
}

void IO::SDK::Scenario::AddSpacecraft(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft)
{
    m_spacecrafts.push_back(&spacecraft);
}

void IO::SDK::Scenario::AddCelestialBody(const IO::SDK::Body::CelestialBody &celestialBody)
{
    m_celestialBodies.push_back(&celestialBody);
}

void IO::SDK::Scenario::AddDistanceConstraint(Constraints::Parameters::DistanceParameters& distanceParameters)
{
    distanceParameters.Order = m_distanceConstraints.size();
    m_distanceConstraints[distanceParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddOccultationConstraint(IO::SDK::Constraints::Parameters::OccultationParameters &occultationParameters)
{
    occultationParameters.Order = m_occultationConstraints.size();
    m_occultationConstraints[occultationParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddDayConstraint(IO::SDK::Constraints::Parameters::ByDayParameters &byDayParameters)
{
    byDayParameters.Order = m_dayConstraints.size();
    m_dayConstraints[byDayParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddNightConstraint(IO::SDK::Constraints::Parameters::ByNightParameters &byNightParameters)
{
    byNightParameters.Order = m_nightConstraints.size();
    m_nightConstraints[byNightParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddBodyVisibilityConstraint(IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters &bodyVisibilityParameters)
{
    bodyVisibilityParameters.Order = m_bodyVisibilityConstraints.size();
    m_bodyVisibilityConstraints[bodyVisibilityParameters] = std::nullopt;
}

void IO::SDK::Scenario::Execute()
{
    // Run sites propagation
    for (const IO::SDK::Sites::Site *site: m_sites)
    {
    }

    //Run bodies propagation

    //Evaluate constraints
}