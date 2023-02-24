//
// Created by s.guillet on 23/02/2023.
//

#include "Scenario.h"

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

void IO::SDK::Scenario::AddDistanceConstraintWindow(
        std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> (*func)(const IO::SDK::Time::Window<IO::SDK::Time::TDB>, const IO::SDK::Body::Body &, const IO::SDK::Body::Body &,
                                                                       const IO::SDK::Constraint &, const IO::SDK::AberrationsEnum, const double, const IO::SDK::Time::TimeSpan &))
{
    m_distanceConstraints[func] = std::nullopt;
}

void IO::SDK::Scenario::AddOccultationConstraintWindow(
        std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>> (*func)(const IO::SDK::Time::Window<IO::SDK::Time::TDB>,
                                                                       const IO::SDK::Body::CelestialBody &, const IO::SDK::Body::CelestialBody &,
                                                                       const IO::SDK::OccultationType &, const IO::SDK::AberrationsEnum,
                                                                       const IO::SDK::Time::TimeSpan &))
{
    m_occultationConstraints[func] = std::nullopt;
}

void IO::SDK::Scenario::AddDayConstraintsWindow(std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> (*func)(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &, const double))
{
    m_dayConstraints[func] = std::nullopt;
}

void IO::SDK::Scenario::AddNightConstraintsWindow(std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> (*func)(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &, const double))
{
    m_nightConstraints[func] = std::nullopt;
}

void IO::SDK::Scenario::AddBodyVisibilityConstraintsWindow(
        std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> (*func)(const IO::SDK::Body::Body &, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &,
                                                                       const IO::SDK::AberrationsEnum))
{
    m_bodyVisibilityConstraints[func] = std::nullopt;
}

void IO::SDK::Scenario::Execute()
{
    // Run sites propagation
    for (const IO::SDK::Sites::Site* site: m_sites)
    {
    }

    //Run bodies propagation

    //Evaluate constraints
}