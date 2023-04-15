#include <algorithm>
#include <iostream>

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
#include "InertialFrames.h"
#include <ApogeeHeightChangingManeuver.h>
#include <PhasingManeuver.h>
#include <InstrumentPointingToAttitude.h>
#include <Launch.h>
#include <GenericKernelsLoader.h>


std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &scenario);

void BuildInstruments(const IO::SDK::API::DTO::ScenarioDTO &scenarioDTO, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildEngines(const IO::SDK::API::DTO::ScenarioDTO &scenarioDTO, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDTO, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

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
                                       std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void LaunchProxy(IO::SDK::API::DTO::LaunchDTO &launchDto)
{
    auto celestialBody = std::make_shared<IO::SDK::Body::CelestialBody>(launchDto.recoverySite.bodyId);
    IO::SDK::Sites::LaunchSite ls(launchDto.launchSite.id, launchDto.launchSite.name, ToGeodetic(launchDto.launchSite.coordinates),
                                  std::make_shared<IO::SDK::Body::CelestialBody>(launchDto.launchSite.bodyId), launchDto.launchSite.directoryPath);
    IO::SDK::Sites::LaunchSite rs(launchDto.recoverySite.id, launchDto.recoverySite.name, ToGeodetic(launchDto.recoverySite.coordinates),
                                  std::make_shared<IO::SDK::Body::CelestialBody>(launchDto.recoverySite.bodyId), launchDto.launchSite.directoryPath);
    IO::SDK::OrbitalParameters::StateVector sv(celestialBody, ToVector3D(launchDto.targetOrbit.position), ToVector3D(launchDto.targetOrbit.velocity),
                                               IO::SDK::Time::TDB(std::chrono::duration<double>(launchDto.targetOrbit.epoch)),
                                               IO::SDK::Frames::Frames(launchDto.targetOrbit.inertialFrame));
    IO::SDK::Maneuvers::Launch launch(ls, rs, launchDto.launchByDay, sv);
    auto res = launch.GetLaunchWindows(ToUTCWindow(launchDto.window));
    for (size_t i = 0; i < res.size(); ++i)
    {
        launchDto.windows[i]= ToWindowDTO(res[i].GetWindow());
    }
}

void PropagateProxy(IO::SDK::API::DTO::ScenarioDTO &scenarioDto)
{
    IO::SDK::Scenario scenario(scenarioDto.Name, ToUTCWindow(scenarioDto.Window));

    //==========Build Celestial bodies=============
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies = BuildCelestialBodies(scenarioDto);
    for (auto &celestial: celestialBodies)
    {
        scenario.AddCelestialBody(*celestial.second);
    }

    //==========Build Spacecraft===============
    auto cbody = celestialBodies[scenarioDto.Spacecraft.initialOrbitalParameter.centerOfMotion.id];
    auto tdb = IO::SDK::Time::TDB(std::chrono::duration<double>(scenarioDto.Spacecraft.initialOrbitalParameter.epoch));
    auto frame = IO::SDK::Frames::InertialFrames(scenarioDto.Spacecraft.initialOrbitalParameter.inertialFrame);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> initialOrbitalParameters = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            cbody,
            ToVector3D(scenarioDto.Spacecraft.initialOrbitalParameter.position),
            ToVector3D(scenarioDto.Spacecraft.initialOrbitalParameter.velocity), tdb, frame);
    IO::SDK::Body::Spacecraft::Spacecraft spacecraft(scenarioDto.Spacecraft.id, scenarioDto.Spacecraft.name,
                                                     scenarioDto.Spacecraft.dryOperatingMass,
                                                     scenarioDto.Spacecraft.maximumOperatingMass, scenarioDto.Name,
                                                     std::move(initialOrbitalParameters));
    BuildFuelTank(scenarioDto, spacecraft);
    BuildEngines(scenarioDto, spacecraft);
    BuildInstruments(scenarioDto, spacecraft);

    scenario.AttachSpacecraft(spacecraft);

    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> maneuvers;

    BuildManeuvers(scenarioDto, scenario, celestialBodies, maneuvers);

    scenario.Execute();
}

std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>>
BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &scenario)
{
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies;

    // insert sun
    for (auto &cb: scenario.CelestialBodies)
    {
        if (cb.id == -1)
        {
            break;
        }
        if (IO::SDK::Body::CelestialBody::IsSun(cb.id))
        {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies[cb.id] = std::make_shared<IO::SDK::Body::CelestialBody>(cb.id);
            break;
        }
    }
    //insert planets or asteroids
    for (auto &cb: scenario.CelestialBodies)
    {
        if (cb.id == -1)
        {
            break;
        }
        if (IO::SDK::Body::CelestialBody::IsAsteroid(cb.id) || IO::SDK::Body::CelestialBody::IsPlanet(cb.id))
        {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id,
                                                                                          celestialBodies[IO::SDK::Body::CelestialBody::FindCenterOfMotionId(
                                                                                                  cb.id)]));
        }
    }

    //insert moons
    for (auto &cb: scenario.CelestialBodies)
    {
        if (cb.id == -1)
        {
            break;
        }
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

void BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft)
{//Add FuelTank
    for (auto &fuelTank: scenarioDto.Spacecraft.fuelTank)
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
    for (auto &engine: scenarioDto.Spacecraft.engines)
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
    for (auto &instrument: scenarioDto.Spacecraft.instruments)
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

void BuildInstrumentPointingToAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                                       std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                                       std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies)
{
    for (auto &maneuver: scenarioDto.Spacecraft.pointingToAttitudes)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
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

    scenario.GetPropagator().SetStandbyManeuver(maneuvers[0].get());
}

void BuildApogeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.Spacecraft.apogeeHeightChangingManeuvers)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
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
    for (auto &maneuver: scenarioDto.Spacecraft.perigeeHeightChangingManeuvers)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
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
    for (auto &maneuver: scenarioDto.Spacecraft.apsidalAlignmentManeuvers)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
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
    for (auto &maneuver: scenarioDto.Spacecraft.combinedManeuvers)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
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
    for (auto &maneuver: scenarioDto.Spacecraft.orbitalPlaneChangingManeuvers)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
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
    for (auto &maneuver: scenarioDto.Spacecraft.phasingManeuverDto)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
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
    for (auto &maneuver: scenarioDto.Spacecraft.progradeAttitudes)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }
        IO::SDK::Time::TDB min(std::chrono::duration<double>(maneuver.minimumEpoch));
        IO::SDK::Time::TimeSpan hold(std::chrono::duration<double>(maneuver.attitudeHoldDuration));
        auto prop = scenario.GetPropagator();
        auto mnv = std::make_shared<IO::SDK::Maneuvers::Attitudes::ProgradeAttitude>(engines, prop,
                                                                                     min, hold);
        maneuvers[maneuver.maneuverOrder] = mnv;
    }
}

void BuildRetrogradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.Spacecraft.retrogradeAttitudes)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::RetrogradeAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)));
    }
}

void BuildNadirAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.Spacecraft.nadirAttitudes)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::NadirAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)));
    }
}

void BuildZenithAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario, std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers)
{
    for (auto &maneuver: scenarioDto.Spacecraft.zenithAttitudes)
    {
        if (maneuver.maneuverOrder == -1)
        {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines)
        {
            if (engine == nullptr)
            {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::ZenithAttitude>(engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                std::chrono::duration<double>(maneuver.minimumEpoch)), IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)));
    }
}


const char *GetSpiceVersionProxy()
{
    const char *version;
    version = tkvrsn_c("TOOLKIT");
    return version;
}

bool WriteEphemerisProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateVectorDTO *sv, int size)
{
    IO::SDK::Kernels::EphemerisKernel kernel(filePath, objectId);

    std::vector<IO::SDK::OrbitalParameters::StateVector> states;
    states.reserve(size);
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies;


    for (int i = 0; i < size; ++i)
    {
        if (celestialBodies.find(sv[0].centerOfMotion.id) == celestialBodies.end())
        {
            celestialBodies[sv[i].centerOfMotion.id] = std::make_shared<IO::SDK::Body::CelestialBody>(sv[i].centerOfMotion.id);
        }
        states.emplace_back(celestialBodies[sv[i].centerOfMotion.id], ToVector3D(sv[i].position), ToVector3D(sv[i].velocity),
                            IO::SDK::Time::TDB(std::chrono::duration<double>(sv[i].epoch)), IO::SDK::Frames::Frames(sv[i].inertialFrame));
    }

    kernel.WriteData(states);

    return true;
}


bool WriteOrientationProxy(const char *filePath, int objectId, int spacecraftFrameId, IO::SDK::API::DTO::StateOrientationDTO *so, int size)
{
    IO::SDK::Kernels::OrientationKernel kernel(filePath, objectId, spacecraftFrameId);
    std::vector<IO::SDK::OrbitalParameters::StateOrientation> orientations;
    orientations.reserve(size);
    for (int i = 0; i < size; ++i)
    {
        orientations.emplace_back(ToQuaternion(so[i].orientation), ToVector3D(so[i].angularVelocity), IO::SDK::Time::TDB(std::chrono::duration<double>(so[i].epoch)),
                                  IO::SDK::Frames::Frames(so[i].frame));
    }
    std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> intervals;
    intervals.push_back(orientations);
    kernel.WriteOrientations(intervals);
    return true;
}

void LoadGenericKernelsProxy(const char *directoryPath)
{
    IO::SDK::Kernels::GenericKernelsLoader::Load(directoryPath);
}
