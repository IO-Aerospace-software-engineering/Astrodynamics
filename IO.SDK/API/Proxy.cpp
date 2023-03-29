#include <Proxy.h>
#include <iostream>
#include <map>
#include <memory>

#include <SpiceUsr.h>
#include <cstring>
#include <Scenario.h>
#include <CelestialBody.h>
#include "Converters.cpp"
#include <TDB.h>
#include <StateVector.h>


std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &s);

IO::SDK::API::DTO::ScenarioDTO Execute(IO::SDK::API::DTO::ScenarioDTO s)
{
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies = BuildCelestialBodies(s);

    IO::SDK::Scenario scenario(s.Name, ToWindow(s.Window));

    //==========BUILD SPACECRAFT===============
    auto cbody = celestialBodies[s.spacecraft.initialOrbitalParameter.centerOfMotion.id];
    auto tdb = IO::SDK::Time::TDB(std::chrono::duration<double>(s.spacecraft.initialOrbitalParameter.epoch));
    std::string frame = s.spacecraft.initialOrbitalParameter.frame;
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> initialOrbitalParameters = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            cbody,
            ToVector3D(s.spacecraft.initialOrbitalParameter.position),
            ToVector3D(s.spacecraft.initialOrbitalParameter.velocity), tdb, frame);

    IO::SDK::Body::Spacecraft::Spacecraft spacecraft(s.spacecraft.id, s.spacecraft.name, s.spacecraft.dryOperatingMass, s.spacecraft.maximumOperatingMass, s.Name,
                                                     std::move(initialOrbitalParameters));

    IO::SDK::API::DTO::ScenarioDTO res;
    res.Name = strdup(s.Name);
    res.Window = s.Window;
//    res.name = "s.name";
//    std::cout << s.Name << std::endl;
//    std::cout << s.Window.start << std::endl;
//    std::cout << s.Window.end << std::endl;
//    res.window = s.window;
//    std::cout << "fueltank 0 id :" << s.spacecraft.fuelTank[0].id << std::endl;
//    std::cout << "fueltank 0 capacity :" << s.spacecraft.fuelTank[0].capacity << std::endl;
//    std::cout << "fueltank 0 quantity :" << s.spacecraft.fuelTank[0].quantity << std::endl;
//
//    std::cout << "engine 0 id :" << s.spacecraft.engines[0].id << std::endl;
//    std::cout << "engine 0 fuelflow :" << s.spacecraft.engines[0].fuelflow << std::endl;
//    std::cout << "engine 0 isp :" << s.spacecraft.engines[0].isp << std::endl;
//
//    std::cout << "occultation 0 aberrationId :" << s.occultations[0].aberrationId << std::endl;
//    std::cout << "occultation 0 backBodyId :" << s.occultations[0].backBodyId << std::endl;
//    std::cout << "occultation 0 bodyId :" << s.occultations[0].observerId << std::endl;
//    std::cout << "occultation 0 frontBodyId :" << s.occultations[0].frontId << std::endl;
//    std::cout << "occultation 0 occultationType :" << s.occultations[0].type << std::endl;
//
//    std::cout << "fov 0 target id :" << s.spacecraft.instruments[0].inFieldOfViews[0].targetId << std::endl;
//    std::cout << "fov 0 instrument id :" << s.spacecraft.instruments[0].id << std::endl;


//    for (size_t i = 0; i < 10; i++)
//    {
//        s.spacecraft.stateVectors[i].position.x = i * 1;
//        s.spacecraft.stateVectors[i].position.y = i * 2;
//        s.spacecraft.stateVectors[i].position.z = i * 3;
//        s.spacecraft.stateVectors[i].velocity.x = i * 4;
//        s.spacecraft.stateVectors[i].velocity.y = i * 5;
//        s.spacecraft.stateVectors[i].velocity.z = i * 6;
//    }
//    return res;

    return res;
}

std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &s)
{
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies;
    for (auto &cb: s.celestialBodies)
    {
        if (cb.id >= 0)
        {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id));
        }
    }
    return celestialBodies;
}

const char *GetSpiceVersionProxy()
{
    const char *version;
    version = tkvrsn_c("TOOLKIT");
    return version;
}

