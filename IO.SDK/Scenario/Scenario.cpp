//
// Created by s.guillet on 23/02/2023.
//

#include <Propagator.h>
#include <Scenario.h>

IO::SDK::Scenario::Scenario(const std::string &name, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windows) : m_name{name}, m_windows{windows},
                                                                                                                 m_integrator(IO::SDK::Parameters::SpacecraftPropagationStep,
                                                                                                                              m_forces)
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

void IO::SDK::Scenario::AddDistanceConstraint(Constraints::Parameters::DistanceParameters *distanceParameters)
{
    m_distanceConstraints[distanceParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddOccultationConstraint(IO::SDK::Constraints::Parameters::OccultationParameters *occultationParameters)
{
    m_occultationConstraints[occultationParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddDayConstraint(IO::SDK::Constraints::Parameters::ByDayParameters *byDayParameters)
{
    m_dayConstraints[byDayParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddNightConstraint(IO::SDK::Constraints::Parameters::ByNightParameters *byNightParameters)
{
    m_nightConstraints[byNightParameters] = std::nullopt;
}

void IO::SDK::Scenario::AddBodyVisibilityConstraint(IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters *bodyVisibilityParameters)
{
    m_bodyVisibilityConstraints[bodyVisibilityParameters] = std::nullopt;
}

void IO::SDK::Scenario::Execute()
{
    // Run sites propagation
    for (auto site: m_sites)
    {
        site->BuildAndWriteEphemeris(this->m_windows);
    }

    auto utc = IO::SDK::Time::Window<IO::SDK::Time::TDB>(m_windows.GetStartDate().ToTDB(), m_windows.GetEndDate().ToTDB());
    //Run bodies propagation
    for (auto spacecraft: m_spacecrafts)
    {
        IO::SDK::Propagators::Propagator propagator(*spacecraft, m_integrator, utc);
    }

    //Evaluate constraints
}