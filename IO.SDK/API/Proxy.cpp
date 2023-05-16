#include <algorithm>

#include <Proxy.h>
#include <Converters.cpp>
#include <PerigeeHeightChangingManeuver.h>
#include <ApsidalAlignmentManeuver.h>
#include <CombinedManeuver.h>
#include <OrbitalPlaneChangingManeuver.h>
#include <ProgradeAttitude.h>
#include <RetrogradeAttitude.h>
#include <NadirAttitude.h>
#include <ZenithAttitude.h>
#include <InertialFrames.h>
#include <ApogeeHeightChangingManeuver.h>
#include <PhasingManeuver.h>
#include <InstrumentPointingToAttitude.h>
#include <Launch.h>
#include <KernelsLoader.h>
#include <iostream>
#include <SpacecraftClockKernel.h>


void LaunchProxy(IO::SDK::API::DTO::LaunchDTO &launchDto) {
    auto celestialBody = std::make_shared<IO::SDK::Body::CelestialBody>(launchDto.recoverySite.bodyId);
    IO::SDK::Sites::LaunchSite ls(launchDto.launchSite.id, launchDto.launchSite.name,
                                  ToGeodetic(launchDto.launchSite.coordinates),
                                  std::make_shared<IO::SDK::Body::CelestialBody>(launchDto.launchSite.bodyId),
                                  launchDto.launchSite.directoryPath);
    IO::SDK::Sites::LaunchSite rs(launchDto.recoverySite.id, launchDto.recoverySite.name,
                                  ToGeodetic(launchDto.recoverySite.coordinates),
                                  std::make_shared<IO::SDK::Body::CelestialBody>(launchDto.recoverySite.bodyId),
                                  launchDto.launchSite.directoryPath);
    IO::SDK::OrbitalParameters::StateVector sv(celestialBody, ToVector3D(launchDto.targetOrbit.position),
                                               ToVector3D(launchDto.targetOrbit.velocity),
                                               IO::SDK::Time::TDB(
                                                       std::chrono::duration<double>(launchDto.targetOrbit.epoch)),
                                               IO::SDK::Frames::Frames(launchDto.targetOrbit.inertialFrame));
    IO::SDK::Maneuvers::Launch launch(ls, rs, launchDto.launchByDay, sv);
    auto res = launch.GetLaunchWindows(ToUTCWindow(launchDto.window));
    for (size_t i = 0; i < res.size(); ++i) {
        launchDto.windows[i] = ToWindowDTO(res[i].GetWindow());
        launchDto.inertialAzimuth = res[i].GetInertialAzimuth();
        launchDto.nonInertialAzimuth = res[i].GetNonInertialAzimuth();
        launchDto.inertialInsertionVelocity = res[i].GetInertialInsertionVelocity();
        launchDto.nonInertialInsertionVelocity = res[i].GetNonInertialInsertionVelocity();
    }
}

void PropagateProxy(IO::SDK::API::DTO::ScenarioDTO &scenarioDto) {
    IO::SDK::Scenario scenario(scenarioDto.Name, ToUTCWindow(scenarioDto.Window));

    //==========Build Celestial bodies=============
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies = BuildCelestialBodies(scenarioDto);
    for (auto &celestial: celestialBodies) {
        scenario.AddCelestialBody(*celestial.second);
    }

//  ==========Build sites==========
    std::vector<std::shared_ptr<IO::SDK::Sites::Site>> sites;
    for (auto &siteDto: scenarioDto.Sites) {
        if (siteDto.id <= 0) {
            break;
        }
        auto site = std::make_shared<IO::SDK::Sites::Site>(siteDto.id, siteDto.name, ToGeodetic(siteDto.coordinates),
                                                           celestialBodies[siteDto.bodyId], siteDto.directoryPath);
        sites.push_back(site);
        scenario.AddSite(*site);
    }


    //==========Build Spacecraft===============
    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> maneuvers;

    auto cbody = celestialBodies[scenarioDto.Spacecraft.initialOrbitalParameter.centerOfMotion.id];
    auto tdb = IO::SDK::Time::TDB(std::chrono::duration<double>(scenarioDto.Spacecraft.initialOrbitalParameter.epoch));
    auto frame = IO::SDK::Frames::InertialFrames(scenarioDto.Spacecraft.initialOrbitalParameter.inertialFrame);
    std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> initialOrbitalParameters = std::make_unique<IO::SDK::OrbitalParameters::StateVector>(
            cbody,
            ToVector3D(scenarioDto.Spacecraft.initialOrbitalParameter.position),
            ToVector3D(scenarioDto.Spacecraft.initialOrbitalParameter.velocity), tdb, frame);
    IO::SDK::Body::Spacecraft::Spacecraft spacecraft(scenarioDto.Spacecraft.id, scenarioDto.Spacecraft.name,
                                                     scenarioDto.Spacecraft.dryOperatingMass,
                                                     scenarioDto.Spacecraft.maximumOperatingMass,
                                                     scenarioDto.Spacecraft.directoryPath,
                                                     std::move(initialOrbitalParameters));
    BuildFuelTank(scenarioDto, spacecraft);
    BuildEngines(scenarioDto, spacecraft);
    BuildInstruments(scenarioDto, spacecraft);
    BuildPayload(scenarioDto, spacecraft);

    scenario.AttachSpacecraft(spacecraft);


    BuildManeuvers(scenarioDto, scenario, celestialBodies, maneuvers);

    scenario.Execute();

    if (!maneuvers.empty()) {
        ReadManeuverResults(scenarioDto, maneuvers);
    }

}

void ReadManeuverResults(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                         std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    ReadApogeeManeuverResult(scenarioDto, maneuvers);

    ReadPerigeeManeuverResult(scenarioDto, maneuvers);

    ReadOrbitalPlaneManeuverResult(scenarioDto, maneuvers);

    ReadCombinedManeuverResult(scenarioDto, maneuvers);

    ReadApsidalAlignmentManeuverResult(scenarioDto, maneuvers);

    ReadPhasingManeuverResult(scenarioDto, maneuvers);
}

void ReadPhasingManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                               std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.phasingManeuverDto) {
        if (maneuver.maneuverOrder < 0) {
            break;
        }

        auto value = maneuvers[maneuver.maneuverOrder];
        maneuver.attitudeWindow = ToWindowDTO(*value->GetAttitudeWindow());
        maneuver.maneuverWindow = ToWindowDTO(*value->GetManeuverWindow());
        maneuver.thrustWindow = ToWindowDTO(*value->GetThrustWindow());
        maneuver.deltaV = ToVector3DDTO(value->GetDeltaV());
        maneuver.FuelBurned = value->GetFuelBurned();
    }
}

void ReadApsidalAlignmentManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                                        std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.apsidalAlignmentManeuvers) {
        if (maneuver.maneuverOrder < 0) {
            break;
        }

        auto value = maneuvers[maneuver.maneuverOrder];
        maneuver.attitudeWindow = ToWindowDTO(*value->GetAttitudeWindow());
        maneuver.maneuverWindow = ToWindowDTO(*value->GetManeuverWindow());
        maneuver.thrustWindow = ToWindowDTO(*value->GetThrustWindow());
        maneuver.deltaV = ToVector3DDTO(value->GetDeltaV());
        maneuver.FuelBurned = value->GetFuelBurned();
    }
}

void ReadCombinedManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                                std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.combinedManeuvers) {
        if (maneuver.maneuverOrder < 0) {
            break;
        }

        auto value = maneuvers[maneuver.maneuverOrder];
        maneuver.attitudeWindow = ToWindowDTO(*value->GetAttitudeWindow());
        maneuver.maneuverWindow = ToWindowDTO(*value->GetManeuverWindow());
        maneuver.thrustWindow = ToWindowDTO(*value->GetThrustWindow());
        maneuver.deltaV = ToVector3DDTO(value->GetDeltaV());
        maneuver.FuelBurned = value->GetFuelBurned();
    }
}

void ReadOrbitalPlaneManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                                    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.orbitalPlaneChangingManeuvers) {
        if (maneuver.maneuverOrder < 0) {
            break;
        }

        auto value = maneuvers[maneuver.maneuverOrder];
        maneuver.attitudeWindow = ToWindowDTO(*value->GetAttitudeWindow());
        maneuver.maneuverWindow = ToWindowDTO(*value->GetManeuverWindow());
        maneuver.thrustWindow = ToWindowDTO(*value->GetThrustWindow());
        maneuver.deltaV = ToVector3DDTO(value->GetDeltaV());
        maneuver.FuelBurned = value->GetFuelBurned();
    }
}

void ReadPerigeeManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                               std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.perigeeHeightChangingManeuvers) {
        if (maneuver.maneuverOrder < 0) {
            break;
        }

        auto value = maneuvers[maneuver.maneuverOrder];
        maneuver.attitudeWindow = ToWindowDTO(*value->GetAttitudeWindow());
        maneuver.maneuverWindow = ToWindowDTO(*value->GetManeuverWindow());
        maneuver.thrustWindow = ToWindowDTO(*value->GetThrustWindow());
        maneuver.deltaV = ToVector3DDTO(value->GetDeltaV());
        maneuver.FuelBurned = value->GetFuelBurned();
    }
}

void ReadApogeeManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                              std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.apogeeHeightChangingManeuvers) {
        if (maneuver.maneuverOrder < 0) {
            break;
        }

        auto value = maneuvers[maneuver.maneuverOrder];
        maneuver.attitudeWindow = ToWindowDTO(*value->GetAttitudeWindow());
        maneuver.maneuverWindow = ToWindowDTO(*value->GetManeuverWindow());
        maneuver.thrustWindow = ToWindowDTO(*value->GetThrustWindow());
        maneuver.deltaV = ToVector3DDTO(value->GetDeltaV());
        maneuver.FuelBurned = value->GetFuelBurned();
    }
}


std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>>
BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &scenario) {
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies;

    // insert sun
    for (auto &cb: scenario.CelestialBodies) {
        if (cb.id == -1) {
            break;
        }
        if (IO::SDK::Body::CelestialBody::IsSun(cb.id)) {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies[cb.id] = std::make_shared<IO::SDK::Body::CelestialBody>(cb.id);
            break;
        }
    }
    //insert planets or asteroids
    for (auto &cb: scenario.CelestialBodies) {
        if (cb.id == -1) {
            break;
        }
        if (IO::SDK::Body::CelestialBody::IsAsteroid(cb.id) || IO::SDK::Body::CelestialBody::IsPlanet(cb.id)) {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id,
                                                                                          celestialBodies[IO::SDK::Body::CelestialBody::FindCenterOfMotionId(
                                                                                                  cb.id)]));
        }
    }

    //insert moons
    for (auto &cb: scenario.CelestialBodies) {
        if (cb.id == -1) {
            break;
        }
        if (IO::SDK::Body::CelestialBody::IsMoon(cb.id)) {
            IO::SDK::Body::CelestialBody c(cb.id);
            celestialBodies.emplace(cb.id, std::make_shared<IO::SDK::Body::CelestialBody>(cb.id,
                                                                                          celestialBodies[IO::SDK::Body::CelestialBody::FindCenterOfMotionId(
                                                                                                  cb.id)]));
        }
    }

    return celestialBodies;
}

void BuildPayload(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                  IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) {//Add FuelTank
    for (auto &payload: scenarioDto.Spacecraft.payloads) {
        if (payload.serialNumber == nullptr) {
            break;
        }
        spacecraft.AddPayload(payload.serialNumber, payload.name, payload.mass);
    }
}

void BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                   IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) {//Add FuelTank
    for (auto &fuelTank: scenarioDto.Spacecraft.fuelTank) {
        if (fuelTank.id == 0) {
            break;
        }
        spacecraft.AddFuelTank(fuelTank.serialNumber, fuelTank.capacity, fuelTank.quantity);
    }
}

void BuildEngines(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                  IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) {//AddEngine
    for (auto &engine: scenarioDto.Spacecraft.engines) {
        if (engine.id == 0) {
            break;
        }
        spacecraft.AddEngine(engine.serialNumber, engine.name, engine.fuelTankSerialNumber,
                             IO::SDK::Math::Vector3D::Zero, IO::SDK::Math::Vector3D::Zero, engine.isp,
                             engine.fuelflow);
    }
}

void BuildInstruments(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                      IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) {//Add instrument
    for (auto &instrument: scenarioDto.Spacecraft.instruments) {
        if (instrument.id <= 0) {
            break;
        }

        if (strcmp(instrument.shape, "rectangular") == 0) {
            spacecraft.AddRectangularFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation),
                                                   ToVector3D(instrument.boresight),
                                                   ToVector3D(instrument.fovRefVector), instrument.fieldOfView,
                                                   instrument.crossAngle);
        }

        if (strcmp(instrument.shape, "circular") == 0) {
            spacecraft.AddCircularFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation),
                                                ToVector3D(instrument.boresight),
                                                ToVector3D(instrument.fovRefVector), instrument.fieldOfView);
        }

        if (strcmp(instrument.shape, "elliptical") == 0) {
            spacecraft.AddEllipticalFOVInstrument(instrument.id, instrument.name, ToVector3D(instrument.orientation),
                                                  ToVector3D(instrument.boresight),
                                                  ToVector3D(instrument.fovRefVector), instrument.fieldOfView,
                                                  instrument.crossAngle);
        }

    }
}

void BuildInstrumentPointingToAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                                       std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                                       std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies) {
    for (auto &maneuver: scenarioDto.Spacecraft.pointingToAttitudes) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }

        auto instrument = scenario.GetSpacecraft()->GetInstrument(maneuver.instrumentId);

        if (maneuver.targetBodyId > -1) {
            auto targetBody = celestialBodies[maneuver.targetBodyId];
            maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude>(
                    engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                            std::chrono::duration<double>(maneuver.minimumEpoch)),
                    IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)), *instrument,
                    *targetBody);
        } else if (maneuver.targetSiteId > -1) {
            auto sites = scenario.GetSites();
            auto site = std::find_if(sites.begin(), sites.end(), [&maneuver](const IO::SDK::Sites::Site *site) {
                return site->GetId() == maneuver.targetSiteId;
            });
            maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::InstrumentPointingToAttitude>(
                    engines, scenario.GetPropagator(), IO::SDK::Time::TDB(
                            std::chrono::duration<double>(maneuver.minimumEpoch)),
                    IO::SDK::Time::TimeSpan(std::chrono::duration<double>(maneuver.attitudeHoldDuration)), *instrument,
                    **site);
        }
    }
}

void BuildManeuvers(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies,
                    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
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

    for (auto &maneuver: maneuvers) {
        if (static_cast<size_t>(maneuver.first) >= maneuvers.size() - 1) {
            continue;
        }
        maneuver.second->SetNextManeuver(*maneuvers[maneuver.first + 1]);
    }

    scenario.GetPropagator().SetStandbyManeuver(maneuvers[0].get());
}

void BuildApogeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                         std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.apogeeHeightChangingManeuvers) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::ApogeeHeightChangingManeuver>(engines,
                                                                                                               scenario.GetPropagator(),
                                                                                                               maneuver.targetHeight,
                                                                                                               IO::SDK::Time::TDB(
                                                                                                                       std::chrono::duration<double>(
                                                                                                                               maneuver.minimumEpoch)));
    }
}

void BuildPerigeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.perigeeHeightChangingManeuvers) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::PerigeeHeightChangingManeuver>(engines,
                                                                                                                scenario.GetPropagator(),
                                                                                                                maneuver.targetHeight,
                                                                                                                IO::SDK::Time::TDB(
                                                                                                                        std::chrono::duration<double>(
                                                                                                                                maneuver.minimumEpoch)));
    }
}

void BuildApsidalManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies) {
    for (auto &maneuver: scenarioDto.Spacecraft.apsidalAlignmentManeuvers) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }
        auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::StateVector>(
                celestialBodies[maneuver.targetOrbit.centerOfMotion.id],
                ToVector3D(maneuver.targetOrbit.position),
                ToVector3D(maneuver.targetOrbit.velocity),
                IO::SDK::Time::TDB(std::chrono::duration<double>(maneuver.targetOrbit.epoch)),
                IO::SDK::Frames::InertialFrames(maneuver.targetOrbit.inertialFrame));
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::ApsidalAlignmentManeuver>(engines,
                                                                                                           scenario.GetPropagator(),
                                                                                                           targetOrbit,
                                                                                                           IO::SDK::Time::TDB(
                                                                                                                   std::chrono::duration<double>(
                                                                                                                           maneuver.minimumEpoch)));
    }
}

void BuildCombinedManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                           std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.combinedManeuvers) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::CombinedManeuver>(engines,
                                                                                                   scenario.GetPropagator(),
                                                                                                   maneuver.targetInclination,
                                                                                                   maneuver.targetHeight,
                                                                                                   IO::SDK::Time::TDB(
                                                                                                           std::chrono::duration<double>(
                                                                                                                   maneuver.minimumEpoch)));
    }
}

void
BuildOrbitalPlaneManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies) {
    for (auto &maneuver: scenarioDto.Spacecraft.orbitalPlaneChangingManeuvers) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }
        auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::StateVector>(
                celestialBodies[maneuver.targetOrbit.centerOfMotion.id],
                ToVector3D(maneuver.targetOrbit.position),
                ToVector3D(maneuver.targetOrbit.velocity),
                IO::SDK::Time::TDB(std::chrono::duration<double>(maneuver.targetOrbit.epoch)),
                IO::SDK::Frames::InertialFrames(maneuver.targetOrbit.inertialFrame));
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::OrbitalPlaneChangingManeuver>(engines,
                                                                                                               scenario.GetPropagator(),
                                                                                                               targetOrbit,
                                                                                                               IO::SDK::Time::TDB(
                                                                                                                       std::chrono::duration<double>(
                                                                                                                               maneuver.minimumEpoch)));
    }
}

void BuildPhasingManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies) {
    for (auto &maneuver: scenarioDto.Spacecraft.phasingManeuverDto) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }
        auto targetOrbit = std::make_shared<IO::SDK::OrbitalParameters::StateVector>(
                celestialBodies[maneuver.targetOrbit.centerOfMotion.id],
                ToVector3D(maneuver.targetOrbit.position),
                ToVector3D(maneuver.targetOrbit.velocity),
                IO::SDK::Time::TDB(std::chrono::duration<double>(maneuver.targetOrbit.epoch)),
                IO::SDK::Frames::InertialFrames(maneuver.targetOrbit.inertialFrame));
        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::PhasingManeuver>(engines,
                                                                                                  scenario.GetPropagator(),
                                                                                                  maneuver.numberRevolutions,
                                                                                                  targetOrbit,
                                                                                                  IO::SDK::Time::TDB(
                                                                                                          std::chrono::duration<double>(
                                                                                                                  maneuver.minimumEpoch)));
    }
}

void BuildProgradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                           std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.progradeAttitudes) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }
        IO::SDK::Time::TDB min(std::chrono::duration<double>(maneuver.minimumEpoch));
        IO::SDK::Time::TimeSpan hold(std::chrono::duration<double>(maneuver.attitudeHoldDuration));
        auto prop = scenario.GetPropagator();
        auto mnv = std::make_shared<IO::SDK::Maneuvers::Attitudes::ProgradeAttitude>(engines, prop,
                                                                                     min, hold);
        maneuvers[maneuver.maneuverOrder] = mnv;
    }
}

void BuildRetrogradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                             std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.retrogradeAttitudes) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::RetrogradeAttitude>(engines,
                                                                                                                scenario.GetPropagator(),
                                                                                                                IO::SDK::Time::TDB(
                                                                                                                        std::chrono::duration<double>(
                                                                                                                                maneuver.minimumEpoch)),
                                                                                                                IO::SDK::Time::TimeSpan(
                                                                                                                        std::chrono::duration<double>(
                                                                                                                                maneuver.attitudeHoldDuration)));
    }
}

void BuildNadirAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                        std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.nadirAttitudes) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::NadirAttitude>(engines,
                                                                                                           scenario.GetPropagator(),
                                                                                                           IO::SDK::Time::TDB(
                                                                                                                   std::chrono::duration<double>(
                                                                                                                           maneuver.minimumEpoch)),
                                                                                                           IO::SDK::Time::TimeSpan(
                                                                                                                   std::chrono::duration<double>(
                                                                                                                           maneuver.attitudeHoldDuration)));
    }
}

void BuildZenithAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                         std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers) {
    for (auto &maneuver: scenarioDto.Spacecraft.zenithAttitudes) {
        if (maneuver.maneuverOrder == -1) {
            break;
        }
        std::vector<IO::SDK::Body::Spacecraft::Engine *> engines;
        for (auto engine: maneuver.engines) {
            if (engine == nullptr) {
                break;
            }
            engines.push_back(const_cast<IO::SDK::Body::Spacecraft::Engine *>(scenario.GetSpacecraft()->GetEngine(
                    strdup(engine))));
        }

        maneuvers[maneuver.maneuverOrder] = std::make_shared<IO::SDK::Maneuvers::Attitudes::ZenithAttitude>(engines,
                                                                                                            scenario.GetPropagator(),
                                                                                                            IO::SDK::Time::TDB(
                                                                                                                    std::chrono::duration<double>(
                                                                                                                            maneuver.minimumEpoch)),
                                                                                                            IO::SDK::Time::TimeSpan(
                                                                                                                    std::chrono::duration<double>(
                                                                                                                            maneuver.attitudeHoldDuration)));
    }
}


const char *GetSpiceVersionProxy() {
    const char *version;
    version = tkvrsn_c("TOOLKIT");
    return strdup(version);
}

bool WriteEphemerisProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateVectorDTO *sv, int size) {
    IO::SDK::Kernels::EphemerisKernel kernel(filePath, objectId);

    std::vector<IO::SDK::OrbitalParameters::StateVector> states;
    states.reserve(size);
    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> celestialBodies;


    for (int i = 0; i < size; ++i) {
        if (celestialBodies.find(sv[0].centerOfMotion.id) == celestialBodies.end()) {
            celestialBodies[sv[i].centerOfMotion.id] = std::make_shared<IO::SDK::Body::CelestialBody>(
                    sv[i].centerOfMotion.id);
        }
        states.emplace_back(celestialBodies[sv[i].centerOfMotion.id], ToVector3D(sv[i].position),
                            ToVector3D(sv[i].velocity),
                            IO::SDK::Time::TDB(std::chrono::duration<double>(sv[i].epoch)),
                            IO::SDK::Frames::Frames(sv[i].inertialFrame));
    }

    kernel.WriteData(states);

    return true;
}


bool WriteOrientationProxy(const char *filePath, int objectId, int spacecraftFrameId,
                           IO::SDK::API::DTO::StateOrientationDTO *so, int size) {
    IO::SDK::Kernels::OrientationKernel kernel(filePath, objectId, spacecraftFrameId);
    std::vector<IO::SDK::OrbitalParameters::StateOrientation> orientations;
    orientations.reserve(size);
    for (int i = 0; i < size; ++i) {
        orientations.emplace_back(ToQuaternion(so[i].orientation), ToVector3D(so[i].angularVelocity),
                                  IO::SDK::Time::TDB(std::chrono::duration<double>(so[i].epoch)),
                                  IO::SDK::Frames::Frames(so[i].frame));
    }
    std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> intervals;
    intervals.push_back(orientations);
    kernel.WriteOrientations(intervals);
    return true;
}

void
ReadOrientationProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int spacecraftId, double tolerance, const char *frame,
                     double stepSize,
                     IO::SDK::API::DTO::StateOrientationDTO *so) {
    if ((searchWindow.end - searchWindow.start) / stepSize > 10000) {
        throw IO::SDK::Exception::InvalidArgumentException(
                "Step size to small or search window to large. The number of State orientation must be lower than 10000");
    }
    //Build platform id
    SpiceInt id = spacecraftId * 1000;

    double epoch = searchWindow.start;
    int idx{0};
    while (epoch <= searchWindow.end) {
        //Get encoded clock
        SpiceDouble sclk = IO::SDK::Kernels::SpacecraftClockKernel::ConvertToEncodedClock(spacecraftId,
                                                                                          IO::SDK::Time::TDB(
                                                                                                  std::chrono::duration<double>(
                                                                                                          epoch)));

        SpiceDouble cmat[3][3];
        SpiceDouble av[3];
        SpiceDouble clkout;
        SpiceBoolean found;

        //Get orientation and angular velocity
        ckgpav_c(id, sclk, tolerance, frame, cmat, av, &clkout, &found);

        if (!found) {
            throw IO::SDK::Exception::SDKException("No orientation found");
        }

        //Build array pointers
        double **arrayCmat;
        arrayCmat = new double *[3];
        for (int i = 0; i < 3; i++) {
            arrayCmat[i] = new double[3]{};
        }

        for (size_t i = 0; i < 3; i++) {
            for (size_t j = 0; j < 3; j++) {
                arrayCmat[i][j] = cmat[i][j];
            }
        }

        IO::SDK::Math::Quaternion q(IO::SDK::Math::Matrix(3, 3, arrayCmat));

        //Free memory
        for (int i = 0; i < 3; i++)
            delete[] arrayCmat[i];
        delete[] arrayCmat;

        double correctedEpoch{};
        sct2e_c(spacecraftId, sclk, &correctedEpoch);
        so[idx].epoch = correctedEpoch;
        so[idx].frame = frame;
        so[idx].orientation = ToQuaternionDTO(q);
        so[idx].angularVelocity.x = av[0];
        so[idx].angularVelocity.y = av[1];
        so[idx].angularVelocity.z = av[2];

        epoch += stepSize;
        idx++;
    }
}

void LoadKernelsProxy(const char *path) {
    IO::SDK::Kernels::KernelsLoader::Load(path);
}

const char *TDBToStringProxy(double secondsFromJ2000) {
    IO::SDK::Time::TDB tdb((std::chrono::duration<double>(secondsFromJ2000)));
    std::string str = tdb.ToString();
    return strdup(str.c_str());
}

const char *UTCToStringProxy(double secondsFromJ2000) {
    IO::SDK::Time::UTC utc((std::chrono::duration<double>(secondsFromJ2000)));
    std::string str = utc.ToString();
    return strdup(str.c_str());
}

void ReadEphemerisProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                        const char *frame,
                        const char *aberration, double stepSize, IO::SDK::API::DTO::StateVectorDTO *stateVectors) {
    if ((searchWindow.end - searchWindow.start) / stepSize > 10000) {
        throw IO::SDK::Exception::InvalidArgumentException(
                "Step size to small or search window to large. The number of State vector must be lower than 10000");
    }
    int idx = 0;
    double epoch = searchWindow.start;
    while (epoch <= searchWindow.end) {

        SpiceDouble vs[6];
        SpiceDouble lt;
        spkezr_c(std::to_string(targetId).c_str(), epoch, frame, aberration, std::to_string(observerId).c_str(), vs,
                 &lt);

        stateVectors[idx].centerOfMotion.id = observerId;
        stateVectors[idx].centerOfMotion.centerOfMotionId = IO::SDK::Body::CelestialBody::FindCenterOfMotionId(
                observerId);

        stateVectors[idx].epoch = epoch;
        stateVectors[idx].inertialFrame = strdup(frame);
        stateVectors[idx].position.x = vs[0] * 1000.0;
        stateVectors[idx].position.y = vs[1] * 1000.0;
        stateVectors[idx].position.z = vs[2] * 1000.0;
        stateVectors[idx].velocity.x = vs[3] * 1000.0;
        stateVectors[idx].velocity.y = vs[4] * 1000.0;
        stateVectors[idx].velocity.z = vs[5] * 1000.0;

        epoch += stepSize;
        idx++;
    }
}

void
FindWindowsOnDistanceConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                                     const char *relationalOperator, double value, const char *aberration,
                                     double stepSize, IO::SDK::API::DTO::WindowDTO windows[1000]) {
    auto relationalOpe = IO::SDK::Constraints::RelationalOperator::ToRelationalOperator(relationalOperator);
    auto abe = IO::SDK::Aberrations::ToEnum(aberration);

    auto res = IO::SDK::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(ToTDBWindow(searchWindow),
                                                                                     observerId, targetId,
                                                                                     relationalOpe, value, abe,
                                                                                     IO::SDK::Time::TimeSpan(stepSize));
    for (size_t i = 0; i < res.size(); ++i) {
        windows[i] = ToWindowDTO(res[i]);
    }
}

void
FindWindowsOnOccultationConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                                        const char *targetFrame, const char *targetShape, int frontBodyId,
                                        const char *frontFrame, const char *frontShape, const char *occultationType,
                                        const char *aberration, double stepSize,
                                        IO::SDK::API::DTO::WindowDTO *windows) {
    auto abe = IO::SDK::Aberrations::ToEnum(aberration);
    auto res = IO::SDK::Constraints::GeometryFinder::FindWindowsOnOccultationConstraint(ToTDBWindow(searchWindow),
                                                                                        observerId, targetId,
                                                                                        targetFrame, targetShape,
                                                                                        frontBodyId,
                                                                                        frontFrame, frontShape,
                                                                                        IO::SDK::OccultationType::ToOccultationType(
                                                                                                occultationType), abe,
                                                                                        IO::SDK::Time::TimeSpan(
                                                                                                stepSize));

    for (size_t i = 0; i < res.size(); ++i) {
        windows[i] = ToWindowDTO(res[i]);
    }
}

void
FindWindowsOnCoordinateConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                                       const char *frame, const char *coordinateSystem,
                                       const char *coordinate,
                                       const char *relationalOperator, double value, double adjustValue,
                                       const char *aberration, double stepSize,
                                       IO::SDK::API::DTO::WindowDTO *windows) {
    auto abe = IO::SDK::Aberrations::ToEnum(aberration);
    auto systemType = IO::SDK::CoordinateSystem::ToCoordinateSystemType(coordinateSystem);
    auto coordinateType = IO::SDK::Coordinate::ToCoordinateType(coordinate);
    auto relationalOpe = IO::SDK::Constraints::RelationalOperator::ToRelationalOperator(relationalOperator);
    auto res = IO::SDK::Constraints::GeometryFinder::FindWindowsOnCoordinateConstraint(ToTDBWindow(searchWindow),
                                                                                       observerId, targetId, frame,
                                                                                       systemType, coordinateType,
                                                                                       relationalOpe, value,
                                                                                       adjustValue, abe,
                                                                                       IO::SDK::Time::TimeSpan(
                                                                                               stepSize));

    for (size_t i = 0; i < res.size(); ++i) {
        windows[i] = ToWindowDTO(res[i]);
    }
}

void FindWindowsOnIlluminationConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId,
                                              const char *illuminationSource, int targetBody, const char *fixedFrame,
                                              IO::SDK::API::DTO::GeodeticDTO geodetic, const char *illuminationType,
                                              const char *relationalOperator, double value,
                                              double adjustValue,
                                              const char *aberration, double stepSize, const char *method,
                                              IO::SDK::API::DTO::WindowDTO *windows) {
    double coordinates[3] = {geodetic.latitude, geodetic.longitude, geodetic.altitude};

    IO::SDK::Body::CelestialBody body(targetBody);
    SpiceDouble bodyFixedLocation[3];
    georec_c(geodetic.longitude, geodetic.latitude, geodetic.altitude, body.GetRadius().GetX(), body.GetFlattening(),
             bodyFixedLocation);
    auto abe = IO::SDK::Aberrations::ToEnum(aberration);
    auto illumination = IO::SDK::IlluminationAngle::ToIlluminationAngleType(illuminationType);
    auto relationalOpe = IO::SDK::Constraints::RelationalOperator::ToRelationalOperator(relationalOperator);
    auto res = IO::SDK::Constraints::GeometryFinder::FindWindowsOnIlluminationConstraint(ToTDBWindow(searchWindow),
                                                                                         observerId, illuminationSource,
                                                                                         targetBody, fixedFrame,
                                                                                         bodyFixedLocation,
                                                                                         illumination, relationalOpe,
                                                                                         value, adjustValue, abe,
                                                                                         IO::SDK::Time::TimeSpan(
                                                                                                 stepSize), method);
    for (size_t i = 0; i < res.size(); ++i) {
        windows[i] = ToWindowDTO(res[i]);
    }
}

void
FindWindowsInFieldOfViewConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int instrumentId,
                                        int targetId, const char *targetFrame,
                                        const char *targetShape,
                                        const char *aberration, double stepSize,
                                        IO::SDK::API::DTO::WindowDTO *windows) {
    auto abe = IO::SDK::Aberrations::ToEnum(aberration);
    auto res = IO::SDK::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(ToTDBWindow(searchWindow),
                                                                                        observerId, instrumentId,
                                                                                        targetId, targetFrame,
                                                                                        targetShape,
                                                                                        abe, IO::SDK::Time::TimeSpan(
                    stepSize));
    for (size_t i = 0; i < res.size(); ++i) {
        windows[i] = ToWindowDTO(res[i]);
    }
}





