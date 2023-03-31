#include <Proxy.h>
#include <Scenario.h>
#include "Converters.cpp"
#include <ApogeeHeightChangingManeuver.h>

std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &s);

void BuildInstruments(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildEngines(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildSpacecraft(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies, IO::SDK::Scenario &scenario);

IO::SDK::API::DTO::ScenarioDTO Execute(IO::SDK::API::DTO::ScenarioDTO &scenarioDto) {
    IO::SDK::Scenario scenario(scenarioDto.Name, ToWindow(scenarioDto.Window));

    //==========Build Celestial bodies=============
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies = BuildCelestialBodies(scenarioDto);
    for (auto &celestial: celestialBodies) {
        scenario.AddCelestialBody(*celestial.second);
    }

    //==========Build spacecraft===============
    auto cbody = celestialBodies[scenarioDto.spacecraft.initialOrbitalParameter.centerOfMotion.id];
    auto tdb = IO::SDK::Time::TDB(std::chrono::duration<double>(scenarioDto.spacecraft.initialOrbitalParameter.epoch));
    auto frame = IO::SDK::Frames::InertialFrames(scenarioDto.spacecraft.initialOrbitalParameter.frame);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> initialOrbitalParameters = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            cbody,
            ToVector3D(scenarioDto.spacecraft.initialOrbitalParameter.position),
            ToVector3D(scenarioDto.spacecraft.initialOrbitalParameter.velocity), tdb, frame);

    IO::SDK::Body::Spacecraft::Spacecraft spacecraft(scenarioDto.spacecraft.id, scenarioDto.spacecraft.name, scenarioDto.spacecraft.dryOperatingMass,
                                                     scenarioDto.spacecraft.maximumOperatingMass, scenarioDto.Name,
                                                     std::move(initialOrbitalParameters));
    BuildFuelTank(scenarioDto, spacecraft);
    BuildEngines(scenarioDto, spacecraft);
    BuildInstruments(scenarioDto, spacecraft);

    scenario.AddSpacecraft(spacecraft);

    IO::SDK::API::DTO::ScenarioDTO res;
    res.Name = strdup(scenarioDto.Name);
    res.Window = scenarioDto.Window;
//    res.name = "scenarioDto.name";
//    std::cout << scenarioDto.Name << std::endl;
//    std::cout << scenarioDto.Window.start << std::endl;
//    std::cout << scenarioDto.Window.end << std::endl;
//    res.window = scenarioDto.window;
//    std::cout << "fueltank 0 id :" << scenarioDto.spacecraft.fuelTank[0].id << std::endl;
//    std::cout << "fueltank 0 capacity :" << scenarioDto.spacecraft.fuelTank[0].capacity << std::endl;
//    std::cout << "fueltank 0 quantity :" << scenarioDto.spacecraft.fuelTank[0].quantity << std::endl;
//
//    std::cout << "engine 0 id :" << scenarioDto.spacecraft.engines[0].id << std::endl;
//    std::cout << "engine 0 fuelflow :" << scenarioDto.spacecraft.engines[0].fuelflow << std::endl;
//    std::cout << "engine 0 isp :" << scenarioDto.spacecraft.engines[0].isp << std::endl;
//
//    std::cout << "occultation 0 aberrationId :" << scenarioDto.occultations[0].aberrationId << std::endl;
//    std::cout << "occultation 0 backBodyId :" << scenarioDto.occultations[0].backBodyId << std::endl;
//    std::cout << "occultation 0 bodyId :" << scenarioDto.occultations[0].observerId << std::endl;
//    std::cout << "occultation 0 frontBodyId :" << scenarioDto.occultations[0].frontId << std::endl;
//    std::cout << "occultation 0 occultationType :" << scenarioDto.occultations[0].type << std::endl;
//
//    std::cout << "fov 0 target id :" << scenarioDto.spacecraft.instruments[0].inFieldOfViews[0].targetId << std::endl;
//    std::cout << "fov 0 instrument id :" << scenarioDto.spacecraft.instruments[0].id << std::endl;


//    for (size_t i = 0; i < 10; i++)
//    {
//        scenarioDto.spacecraft.stateVectors[i].position.x = i * 1;
//        scenarioDto.spacecraft.stateVectors[i].position.y = i * 2;
//        scenarioDto.spacecraft.stateVectors[i].position.z = i * 3;
//        scenarioDto.spacecraft.stateVectors[i].velocity.x = i * 4;
//        scenarioDto.spacecraft.stateVectors[i].velocity.y = i * 5;
//        scenarioDto.spacecraft.stateVectors[i].velocity.z = i * 6;
//    }
//    return res;

    return res;
}

void BuildSpacecraft(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies, IO::SDK::Scenario &scenario) {
    auto cbody = celestialBodies[scenarioDto.spacecraft.initialOrbitalParameter.centerOfMotion.id];
    auto tdb = IO::SDK::Time::TDB(std::chrono::duration<double>(scenarioDto.spacecraft.initialOrbitalParameter.epoch));
    auto frame = IO::SDK::Frames::InertialFrames(scenarioDto.spacecraft.initialOrbitalParameter.frame);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> initialOrbitalParameters = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            cbody,
            ToVector3D(scenarioDto.spacecraft.initialOrbitalParameter.position),
            ToVector3D(scenarioDto.spacecraft.initialOrbitalParameter.velocity), tdb, frame);

    IO::SDK::Body::Spacecraft::Spacecraft spacecraft(scenarioDto.spacecraft.id, scenarioDto.spacecraft.name, scenarioDto.spacecraft.dryOperatingMass,
                                                     scenarioDto.spacecraft.maximumOperatingMass, scenarioDto.Name,
                                                     std::move(initialOrbitalParameters));
    BuildFuelTank(scenarioDto, spacecraft);
    BuildEngines(scenarioDto, spacecraft);
    BuildInstruments(scenarioDto, spacecraft);

    scenario.AddSpacecraft(spacecraft);
}

void BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) {//Add FuelTank
    for (int i = 0; i < 5; ++i) {
        auto fuelTank = scenarioDto.spacecraft.fuelTank[i];
        if (fuelTank.id == 0) {
            break;
        }
        spacecraft.AddFuelTank(fuelTank.serialNumber, fuelTank.capacity, fuelTank.quantity);
    }
}

void BuildEngines(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) {//AddEngine
    for (int i = 0; i < 5; ++i) {
        auto engine = scenarioDto.spacecraft.engines[i];
        if (engine.id == 0) {
            break;
        }
        spacecraft.AddEngine(engine.serialNumber, engine.name, engine.fuelTankSerialNumber, IO::SDK::Math::Vector3D::Zero, IO::SDK::Math::Vector3D::Zero, engine.isp,
                             engine.fuelflow);
    }
}

void BuildInstruments(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) {//Add instrument
    for (int i = 0; i < 5; ++i) {
        auto instrument = scenarioDto.spacecraft.instruments[i];
        if (instrument.id <= 0) {
            break;
        }

        if (strcmp(instrument.shape, "rectangular") == 0) {
            spacecraft.AddRectangularFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation), ToVector3D(instrument.boresight),
                                                   ToVector3D(instrument.fovRefVector), instrument.fieldOfView, instrument.crossAngle);
        }

        if (strcmp(instrument.shape, "circular") == 0) {
            spacecraft.AddCircularFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation), ToVector3D(instrument.boresight),
                                                ToVector3D(instrument.fovRefVector), instrument.fieldOfView);
        }

        if (strcmp(instrument.shape, "elliptical") == 0) {
            spacecraft.AddEllipticalFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation), ToVector3D(instrument.boresight),
                                                  ToVector3D(instrument.fovRefVector), instrument.fieldOfView, instrument.crossAngle);
        }

    }
}

std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &scenario) {
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies;

    // insert sun
    for (auto &cb: scenario.celestialBodies) {
        if (IO::SDK::Body::CelestialBody::IsSun(cb.id)) {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies[cb.id] = std::make_shared<IO::SDK::Body::CelestialBody>(cb.id);
            break;
        }
    }
    //insert planets or asteroids
    for (auto &cb: scenario.celestialBodies) {
        if (IO::SDK::Body::CelestialBody::IsAsteroid(cb.id) || IO::SDK::Body::CelestialBody::IsPlanet(cb.id)) {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id, celestialBodies[IO::SDK::Body::CelestialBody::FindCenterOfMotionId(cb.id)]));
        }
    }

    //insert moons
    for (auto &cb: scenario.celestialBodies) {
        if (IO::SDK::Body::CelestialBody::IsMoon(cb.id)) {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id, celestialBodies[IO::SDK::Body::CelestialBody::FindCenterOfMotionId(cb.id)]));
        }
    }

    return celestialBodies;
}


const char *GetSpiceVersionProxy() {
    const char *version;
    version = tkvrsn_c("TOOLKIT");
    return version;
}

