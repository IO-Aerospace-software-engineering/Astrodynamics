/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 23/02/2023.
//

#include <Scenario.h>
#include <Launch.h>

#include <Parameters.h>

IO::SDK::Scenario::Scenario(std::string name, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windows) : m_name{std::move(name)}, m_windows{windows},
                                                                                                          m_integrator(IO::SDK::Parameters::SpacecraftPropagationStep,
                                                                                                                       m_forces)
{

}

void IO::SDK::Scenario::AddSite(const IO::SDK::Sites::Site &site)
{
    m_sites.push_back(&site);
}

void IO::SDK::Scenario::AttachSpacecraft(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft)
{
    m_spacecraft = &spacecraft;
    m_propagator = std::make_unique<Propagators::Propagator>(*m_spacecraft, m_integrator,
                                                             IO::SDK::Time::Window<Time::TDB>(m_windows.GetStartDate().ToTDB(), m_windows.GetEndDate().ToTDB()));
}

void IO::SDK::Scenario::AddCelestialBody(const IO::SDK::Body::CelestialBody &celestialBody)
{
    m_celestialBodies.push_back(&celestialBody);
}

void IO::SDK::Scenario::Execute()
{
    // Run Sites propagation
    for (auto site: m_sites)
    {
        site->BuildAndWriteEphemeris(this->m_windows);
    }

    auto tdb = IO::SDK::Time::Window<IO::SDK::Time::TDB>(m_windows.GetStartDate().ToTDB(), m_windows.GetEndDate().ToTDB());
    //Run bodies propagation
    if (m_spacecraft)
    {
        m_propagator->Propagate();
    }
}




