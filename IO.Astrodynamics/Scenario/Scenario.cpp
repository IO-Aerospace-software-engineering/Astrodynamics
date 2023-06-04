/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 23/02/2023.
//

#include <Scenario.h>
#include <Launch.h>

#include <Parameters.h>

IO::Astrodynamics::Scenario::Scenario(std::string name, const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &windows) : m_name{std::move(name)}, m_windows{windows},
                                                                                                          m_integrator(IO::Astrodynamics::Parameters::SpacecraftPropagationStep,
                                                                                                                       m_forces)
{

}

void IO::Astrodynamics::Scenario::AddSite(const IO::Astrodynamics::Sites::Site &site)
{
    m_sites.push_back(&site);
}

void IO::Astrodynamics::Scenario::AttachSpacecraft(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft)
{
    m_spacecraft = &spacecraft;
    m_propagator = std::make_unique<Propagators::Propagator>(*m_spacecraft, m_integrator,
                                                             IO::Astrodynamics::Time::Window<Time::TDB>(m_windows.GetStartDate().ToTDB(), m_windows.GetEndDate().ToTDB()));
}

void IO::Astrodynamics::Scenario::AddCelestialBody(const IO::Astrodynamics::Body::CelestialBody &celestialBody)
{
    m_celestialBodies.push_back(&celestialBody);
}

void IO::Astrodynamics::Scenario::Execute()
{
    // Run Sites propagation
    for (auto site: m_sites)
    {
        site->BuildAndWriteEphemeris(this->m_windows);
    }

    auto tdb = IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>(m_windows.GetStartDate().ToTDB(), m_windows.GetEndDate().ToTDB());
    //Run bodies propagation
    if (m_spacecraft)
    {
        m_propagator->Propagate();
    }
}




