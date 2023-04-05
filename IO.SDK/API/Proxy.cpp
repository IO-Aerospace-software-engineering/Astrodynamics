#include <Proxy.h>
#include <Scenario.h>
#include "Converters.cpp"
#include "PerigeeHeightChangingManeuver.h"
#include "ApsidalAlignmentManeuver.h"
#include "CombinedManeuver.h"
#include "OrbitalPlaneChangingManeuver.h"
#include "ProgradeAttitude.h"
#include "RetrogradeAttitude.h"
#include "NadirAttitude.h"
#include "ZenithAttitude.h"
#include <ApogeeHeightChangingManeuver.h>
#include <PhasingManeuver.h>
#include <InstrumentPointingToAttitude.h>
#include <algorithm>

std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &s);

void BuildInstruments(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildEngines(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildApogeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildPerigeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildApsidalManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void BuildCombinedManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void
BuildOrbitalPlaneManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void BuildPhasingManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void BuildManeuvers(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies,
                    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildProgradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildRetrogradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildZenithAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildNadirAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildInstrumentPointingToAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                                       std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                                       const std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies
                                       );

IO::SDK::API::DTO::ScenarioDTO Execute(IO::SDK::API::DTO::ScenarioDTO &scenarioDto)
{
    IO::SDK::Scenario scenario(scenarioDto.Name, ToWindow(scenarioDto.Window));

    //==========Build Celestial bodies=============
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies = BuildCelestialBodies(scenarioDto);
    for (auto &celestial: celestialBodies)
    {
        scenario.AddCelestialBody(*celestial.second);
    }

    //==========Build spacecraft===============
    auto cbody = celestialBodies[scenarioDto.spacecraft.initialOrbitalParameter.centerOfMotion.id];
    auto tdb = IO::SDK::Time::TDB(std::chrono::duration<double>(scenarioDto.spacecraft.initialOrbitalParameter.epoch));
    auto frame = IO::SDK::Frames::InertialFrames(scenarioDto.spacecraft.initialOrbitalParameter.inertialFrame);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> initialOrbitalParameters = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            cbody,
            ToVector3D(scenarioDto.spacecraft.initialOrbitalParameter.position),
            ToVector3D(scenarioDto.spacecraft.initialOrbitalParameter.velocity), tdb, frame);

    IO::SDK::Body::Spacecraft::Spacecraft spacecraft(scenarioDto.spacecraft.id, scenarioDto.spacecraft.name,
                                                     scenarioDto.spacecraft.dryOperatingMass,
                                                     scenarioDto.spacecraft.maximumOperatingMass, scenarioDto.Name,
                                                     std::move(initialOrbitalParameters));
    BuildFuelTank(scenarioDto, spacecraft);
    BuildEngines(scenarioDto, spacecraft);
    BuildInstruments(scenarioDto, spacecraft);

    scenario.AttachSpacecraft(spacecraft);

    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> maneuvers;

    BuildManeuvers(scenarioDto, scenario, celestialBodies, maneuvers);

    IO::SDK::API::DTO::ScenarioDTO res;
    res.Name = strdup(scenarioDto.Name);
    res.Window = scenarioDto.Window;

    return res;
}

void BuildInstrumentPointingToAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                                       std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                                       std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies)
{
    for (auto &maneuver: scenarioDto.spacecraft.pointingToAttitudes)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        auto instrument = scenario.GetSpacecraft()->GetInstrument(maneuver.instrumentId);

        if (maneuver.targetBodyId > -1)
        {
            auto targetBody = celestialBodies[maneuver.targetBodyId];
            maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                                                                                                                                      std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)), *instrument,
                                                                                                                              *targetBody);
        } else if (maneuver.targetSiteId > -1)
        {
            auto sites = scenario.GetSites();
            auto site = std::find_if(sites.begin(), sites.end(), [&maneuver](const IO::SDK::Sites::Site *site) { return site->GetId() == maneuver.targetSiteId; });
            maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                                                                                                                                      std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)), *instrument,
                                                                                                                              **site);
        }
    }
}

void BuildManeuvers(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies,
                    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    BuildApogeeManeuver(scenarioDto, scenario, maneuvers);
    BuildPerigeeManeuver(scenarioDto, scenario, maneuvers);
    BuildCombinedManeuver(scenarioDto, scenario, maneuvers);
    BuildApsidalManeuver(scenarioDto, scenario, maneuvers, celestialBodies);
    BuildOrbitalPlaneManeuver(scenarioDto, scenario, maneuvers, celestialBodies);
    BuildPhasingManeuver(scenarioDto, scenario, maneuvers, celestialBodies);
    BuildProgradeAttitude(scenarioDto, scenario, maneuvers);
    BuildRetrogradeAttitude(scenarioDto, scenario, maneuvers);
    BuildZenithAttitude(scenarioDto, scenario, maneuvers);
    BuildNadirAttitude(scenarioDto, scenario, maneuvers);
    BuildInstrumentPointingToAttitude(scenarioDto, scenario, maneuvers, celestialBodies);

    for (auto &maneuver: maneuvers)
    {
        if (static_cast<size_t>(maneuver.first) >= maneuvers.size() - 1)
        {
            continue;
        }
        maneuver.second->SetNextManeuver(*maneuvers[maneuver.first + 1]);
    }
}

void BuildApogeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.spacecraft.apogeeHeightChangingManeuvers)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::ApogeeHeightChangingManeuver>(engines,
                                                                                                               scenario.GetPropagator(),
                                                                                                               maneuver.targetHeight, IO::SDK::Time::TDB(
                        std::chrono::duration<double>(maneuver.minimumEpoch)));
    }
}

void BuildPerigeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.spacecraft.perigeeHeightChangingManeuvers)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::PerigeeHeightChangingManeuver>(engines,
                                                                                                                scenario.GetPropagator(),
                                                                                                                maneuver.targetHeight, IO::SDK::Time::TDB(
                        std::chrono::duration<double>(maneuver.minimumEpoch)));
    }
}

void BuildApsidalManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies)
{
    for (auto &maneuver: scenarioDto.spacecraft.apsidalAlignmentManeuvers)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }
        auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::StateVector>(celestialBodies[maneuver.targetOrbit.centerOfMotion.id],
                                                                                     ToVector3D(maneuver.targetOrbit.position),
                                                                                     ToVector3D(maneuver.targetOrbit.velocity),
                                                                                     IO::SDK::Time::TDB(std::chrono::duration<double>(maneuver.targetOrbit.epoch)),
                                                                                     IO::SDK::Frames::InertialFrames(maneuver.targetOrbit.inertialFrame));
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::ApsidalAlignmentManeuver>(engines,
                                                                                                           scenario.GetPropagator(),
                                                                                                           targetOrbit, IO::SDK::Time::TDB(
                        std::chrono::duration<double>(maneuver.minimumEpoch)));
    }
}

void BuildCombinedManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.spacecraft.combinedManeuvers)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::CombinedManeuver>(engines,
                                                                                                   scenario.GetPropagator(), maneuver.targetInclination,
                                                                                                   maneuver.targetHeight, IO::SDK::Time::TDB(
                        std::chrono::duration<double>(maneuver.minimumEpoch)));
    }
}

void
BuildOrbitalPlaneManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies)
{
    for (auto &maneuver: scenarioDto.spacecraft.orbitalPlaneChangingManeuvers)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }
        auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::StateVector>(celestialBodies[maneuver.targetOrbit.centerOfMotion.id],
                                                                                     ToVector3D(maneuver.targetOrbit.position),
                                                                                     ToVector3D(maneuver.targetOrbit.velocity),
                                                                                     IO::SDK::Time::TDB(std::chrono::duration<double>(maneuver.targetOrbit.epoch)),
                                                                                     IO::SDK::Frames::InertialFrames(maneuver.targetOrbit.inertialFrame));
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver>(engines,
                                                                                                               scenario.GetPropagator(),
                                                                                                               targetOrbit, IO::SDK::Time::TDB(
                        std::chrono::duration<double>(maneuver.minimumEpoch)));
    }
}

void BuildPhasingManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies)
{
    for (auto &maneuver: scenarioDto.spacecraft.phasingManeuverDto)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }
        auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::StateVector>(celestialBodies[maneuver.targetOrbit.centerOfMotion.id],
                                                                                     ToVector3D(maneuver.targetOrbit.position),
                                                                                     ToVector3D(maneuver.targetOrbit.velocity),
                                                                                     IO::SDK::Time::TDB(std::chrono::duration<double>(maneuver.targetOrbit.epoch)),
                                                                                     IO::SDK::Frames::InertialFrames(maneuver.targetOrbit.inertialFrame));
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::PhasingManeuver>(engines, scenario.GetPropagator(), maneuver.numberRevolutions,
                                                                                                  targetOrbit, IO::SDK::Time::TDB(
                        std::chrono::duration<double>(maneuver.minimumEpoch)));
    }
}

void BuildProgradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.spacecraft.progradeAttitudes)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::ProgradeAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)));
    }
}

void BuildRetrogradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.spacecraft.retrogradeAttitudes)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::RetrogradeAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)));
    }
}

void BuildNadirAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.spacecraft.nadirAttitudes)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::NadirAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)));
    }
}

void BuildZenithAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.spacecraft.zenithAttitudes)
    {
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::ZenithAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)));
    }
}

void BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft)
{//Add FuelTank
    for (auto &fuelTank: scenarioDto.spacecraft.fuelTank)
    {
        if (fuelTank.id == 0)
        {
            break;
        }
        spacecraft.AddFuelTank(fuelTank.serialNumber, fuelTank.capacity, fuelTank.quantity);
    }
}

void BuildEngines(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                  IO::SDK::Body::Spacecraft::Spacecraft &spacecraft)
{//AddEngine
    for (auto &engine: scenarioDto.spacecraft.engines)
    {
        if (engine.id == 0)
        {
            break;
        }
        spacecraft.AddEngine(engine.serialNumber, engine.name, engine.fuelTankSerialNumber,
                             IO::SDK::Math::Vector3D::Zero, IO::SDK::Math::Vector3D::Zero, engine.isp,
                             engine.fuelflow);
    }
}

void BuildInstruments(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                      IO::SDK::Body::Spacecraft::Spacecraft &spacecraft)
{//Add instrument
    for (auto &instrument: scenarioDto.spacecraft.instruments)
    {
        if (instrument.id <= 0)
        {
            break;
        }

        if (strcmp(instrument.shape, "rectangular") == 0)
        {
            spacecraft.AddRectangularFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation),
                                                   ToVector3D(instrument.boresight),
                                                   ToVector3D(instrument.fovRefVector), instrument.fieldOfView,
                                                   instrument.crossAngle);
        }

        if (strcmp(instrument.shape, "circular") == 0)
        {
            spacecraft.AddCircularFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation),
                                                ToVector3D(instrument.boresight),
                                                ToVector3D(instrument.fovRefVector), instrument.fieldOfView);
        }

        if (strcmp(instrument.shape, "elliptical") == 0)
        {
            spacecraft.AddEllipticalFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation),
                                                  ToVector3D(instrument.boresight),
                                                  ToVector3D(instrument.fovRefVector), instrument.fieldOfView,
                                                  instrument.crossAngle);
        }

    }
}

std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>>
BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &scenario)
{
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies;

    // insert sun
    for (auto &cb: scenario.celestialBodies)
    {
        if (IO::SDK::Body::CelestialBody::IsSun(cb.id))
        {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies[cb.id] = std::make_shared<IO::SDK::Body::CelestialBody>(cb.id);
            break;
        }
    }
    //insert planets or asteroids
    for (auto &cb: scenario.celestialBodies)
    {
        if (IO::SDK::Body::CelestialBody::IsAsteroid(cb.id) || IO::SDK::Body::CelestialBody::IsPlanet(cb.id))
        {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id,
                                                                                          celestialBodies[IO::SDK::Body::CelestialBody::FindCenterOfMotionId(
                                                                                                  cb.id)]));
        }
    }

    //insert moons
    for (auto &cb: scenario.celestialBodies)
    {
        if (IO::SDK::Body::CelestialBody::IsMoon(cb.id))
        {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id,
                                                                                          celestialBodies[IO::SDK::Body::CelestialBody::FindCenterOfMotionId(
                                                                                                  cb.id)]));
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

