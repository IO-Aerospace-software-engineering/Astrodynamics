
#include <ScenarioDTO.h>
#include "ManeuverBase.h"
#include "Scenario.h"

#ifdef __cplusplus
extern "C" {
#endif
#ifdef _WIN32
#  ifdef MODULE_API_EXPORTS
#    define MODULE_API __declspec(dllexport)
#  else
#    define MODULE_API __declspec(dllimport)
#  endif
#else
#  define MODULE_API
#endif
/**
 * Get the spice version
 * @return version
 */
MODULE_API const char *GetSpiceVersionProxy();

/**
 * Write ephemeris into binary file (spk)
 * @param filePath
 * @param objectId
 * @param sv
 * @param size
 * @return
 */
MODULE_API bool
WriteEphemerisProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateVectorDTO sv[100000], int size);

/**
 * Read object ephemeris
 * @param searchWindow
 * @param observerId
 * @param targetId
 * @param frame
 * @param aberration
 * @param stepSize
 * @param stateVectors
 */
MODULE_API void
ReadEphemerisProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int targetId, const char *frame,
                   const char *aberration, double stepSize, IO::SDK::API::DTO::StateVectorDTO stateVectors[10000]);

/**
 * Write orientation data into binary file (ck)
 * @param filePath
 * @param objectId
 * @param so
 * @param size
 * @return
 */
MODULE_API bool
WriteOrientationProxy(const char *filePath, int objectId, IO::SDK::API::DTO::StateOrientationDTO so[10000], int size);

/**
 * Read spacecraft orientation
 * @param searchWindow
 * @param spacecraftId
 * @param tolerance
 * @param frame
 * @param stepSize
 * @param so
 */
MODULE_API void
ReadOrientationProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int spacecraftId, double tolerance, const char *frame, double stepSize,
                     IO::SDK::API::DTO::StateOrientationDTO so[10000]);

/**
 * Propagate a scenario
 * @param scenarioDto
 */
MODULE_API void PropagateProxy(IO::SDK::API::DTO::ScenarioDTO &scenarioDto);



/**
 * Evaluate launch windows
 * @param launchDto
 */
MODULE_API void LaunchProxy(IO::SDK::API::DTO::LaunchDTO &launchDto);

/**
 * Load generic kernels
 * @param directoryPath
 */
MODULE_API void LoadKernelsProxy(const char *path);

/**
 * Convert secondFromJ2000 to formatted string
 * @param secondsFromJ2000
 * @return
 */
MODULE_API const char *TDBToStringProxy(double secondsFromJ2000);

/**
 * Convert secondFromJ2000 to formatted string
 * @param secondsFromJ2000
 * @return
 */
MODULE_API const char *UTCToStringProxy(double secondsFromJ2000);

/**
 * Find time windows witch satisfy distance constraint
 * @param searchWindow
 * @param observerId
 * @param targetId
 * @param relationalOperator
 * @param value
 * @param aberration
 * @param stepSize
 * @param windows
 */
MODULE_API void
FindWindowsOnDistanceConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                                     const char *relationalOperator, double value, const char *aberration,
                                     double stepSize, IO::SDK::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows witch satisfy occultation constraint
 * @param searchWindow
 * @param observerId
 * @param targetId
 * @param targetFrame
 * @param targetShape
 * @param frontBodyId
 * @param frontFrame
 * @param frontShape
 * @param occultationType
 * @param aberration
 * @param stepSize
 * @param windows
 */
MODULE_API void FindWindowsOnOccultationConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId,
                                                        int targetId, const char *targetFrame,
                                                        const char *targetShape,
                                                        int frontBodyId, const char *frontFrame, const char *frontShape,
                                                        const char *occultationType,
                                                        const char *aberration, double stepSize,
                                                        IO::SDK::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows witch satisfy coordinate constraint
 * @param searchWindow
 * @param observerId
 * @param targetId
 * @param frame
 * @param coordinateSystem
 * @param coordinate
 * @param relationalOperator
 * @param value
 * @param adjustValue
 * @param aberration
 * @param stepSize
 * @param windows
 */
MODULE_API void FindWindowsOnCoordinateConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId,
                                                       int targetId, const char *frame, const char *coordinateSystem,
                                                       const char *coordinate, const char *relationalOperator,
                                                       double value, double adjustValue, const char *aberration,
                                                       double stepSize, IO::SDK::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows witch satisfy illumination constraint
 * @param searchWindow
 * @param observerId
 * @param illuminationSource
 * @param targetBody
 * @param fixedFrame
 * @param geodetic
 * @param illuminationType
 * @param relationalOperator
 * @param value
 * @param adjustValue
 * @param aberration
 * @param stepSize
 * @param method
 * @param windows
 */
MODULE_API void FindWindowsOnIlluminationConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId,
                                                         const char *illuminationSource, int targetBody,
                                                         const char *fixedFrame,
                                                         IO::SDK::API::DTO::GeodeticDTO geodetic,
                                                         const char *illuminationType,
                                                         const char *relationalOperator, double value,
                                                         double adjustValue,
                                                         const char *aberration, double stepSize, const char *method,
                                                         IO::SDK::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows witch satisfy in field of view constraint
 * @param searchWindow
 * @param observerId
 * @param instrumentId
 * @param targetId
 * @param targetFrame
 * @param targetShape
 * @param aberration
 * @param stepSize
 * @param windows
 */
MODULE_API void
FindWindowsInFieldOfViewConstraintProxy(IO::SDK::API::DTO::WindowDTO searchWindow, int observerId, int instrumentId,
                                        int targetId, const char *targetFrame, const char *targetShape,
                                        const char *aberration, double stepSize,
                                        IO::SDK::API::DTO::WindowDTO windows[1000]);
/**
 * Convert elapsed seconds from J2000 to UTC
 * @param tdb
 * @return
 */
MODULE_API double ConvertTDBToUTCProxy(double tdb);

/**
 * Convert elapsed seconds from J2000 to tdb
 * @param utc
 * @return
 */
MODULE_API double ConvertUTCToTDBProxy(double utc);
#ifdef __cplusplus
}
#endif

#ifndef PROXY_H
#define PROXY_H

std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>>
BuildCelestialBodies(IO::SDK::API::DTO::ScenarioDTO &scenario);

void BuildPayload(const IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void
BuildInstruments(const IO::SDK::API::DTO::ScenarioDTO &scenarioDTO, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildEngines(const IO::SDK::API::DTO::ScenarioDTO &scenarioDTO, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void
BuildFuelTank(const IO::SDK::API::DTO::ScenarioDTO &scenarioDTO, IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

void BuildApogeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                         std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildPerigeeManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildApsidalManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void BuildCombinedManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                           std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void
BuildOrbitalPlaneManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void BuildPhasingManeuver(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                          std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                          std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void BuildManeuvers(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                    std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies,
                    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildProgradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                           std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildRetrogradeAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                             std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildZenithAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                         std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildNadirAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                        std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void BuildInstrumentPointingToAttitude(IO::SDK::API::DTO::ScenarioDTO &scenarioDto, IO::SDK::Scenario &scenario,
                                       std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers,
                                       std::map<int, std::shared_ptr<IO::SDK::Body::CelestialBody>> &celestialBodies);

void ReadApogeeManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                              std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void ReadPerigeeManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                               std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void ReadOrbitalPlaneManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                                    std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void ReadCombinedManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                                std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void ReadApsidalAlignmentManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                                        std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void ReadPhasingManeuverResult(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                               std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

void ReadManeuverResults(IO::SDK::API::DTO::ScenarioDTO &scenarioDto,
                         std::map<int, std::shared_ptr<IO::SDK::Maneuvers::ManeuverBase>> &maneuvers);

#endif
