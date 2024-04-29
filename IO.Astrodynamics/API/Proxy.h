
/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <FrameTransformation.h>
#include <ConicOrbitalElementsDTO.h>
#include <EquinoctialElementsDTO.h>
#include <RaDecDTO.h>
#include <HorizontalDTO.h>
#include <TLEElementsDTO.h>
#include "CelestialBody.h"
#include "StateVectorDTO.h"
#include "StateOrientationDTO.h"
#include "WindowDTO.h"
#include "SiteDTO.h"
#include "LaunchDTO.h"

#pragma region Proxy
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
WriteEphemerisProxy(const char *filePath, int objectId, IO::Astrodynamics::API::DTO::StateVectorDTO sv[100000], unsigned int size);

/**
 * Write orientation into binary file (ck)
 * @param filePath
 * @param objectId
 * @param so
 * @param size
 * @return
 */
MODULE_API bool WriteOrientationProxy(const char *filePath, int objectId, IO::Astrodynamics::API::DTO::StateOrientationDTO so[100000], unsigned int size);

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
ReadEphemerisProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId, int targetId, const char *frame,
                   const char *aberration, double stepSize, IO::Astrodynamics::API::DTO::StateVectorDTO stateVectors[10000]);

MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ReadEphemerisAtGivenEpochProxy(double epoch, int observerId, int targetId, const char *frame, const char *aberration);

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
ReadOrientationProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int spacecraftId, double tolerance, const char *frame, double stepSize,
                     IO::Astrodynamics::API::DTO::StateOrientationDTO so[10000]);

/**
 * Evaluate launch windows
 * @param launchDto
 */
MODULE_API void LaunchProxy(IO::Astrodynamics::API::DTO::LaunchDTO &launchDto);

/**
 * Load kernels
 * @param directoryPath
 */
MODULE_API bool LoadKernelsProxy(const char *path);

/**
 * Unload kernels
 * @param directoryPath
 */
MODULE_API bool UnloadKernelsProxy(const char *path);

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
FindWindowsOnDistanceConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                                     const char *relationalOperator, double value, const char *aberration,
                                     double stepSize, IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

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
MODULE_API void FindWindowsOnOccultationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                                        int targetId, const char *targetFrame,
                                                        const char *targetShape,
                                                        int frontBodyId, const char *frontFrame, const char *frontShape,
                                                        const char *occultationType,
                                                        const char *aberration, double stepSize,
                                                        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

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
MODULE_API void FindWindowsOnCoordinateConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                                       int targetId, const char *frame, const char *coordinateSystem,
                                                       const char *coordinate, const char *relationalOperator,
                                                       double value, double adjustValue, const char *aberration,
                                                       double stepSize, IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

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
MODULE_API void FindWindowsOnIlluminationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                                         const char *illuminationSource, int targetBody,
                                                         const char *fixedFrame,
                                                         IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic,
                                                         const char *illuminationType,
                                                         const char *relationalOperator, double value,
                                                         double adjustValue,
                                                         const char *aberration, double stepSize, const char *method,
                                                         IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

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
FindWindowsInFieldOfViewConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId, int instrumentId,
                                        int targetId, const char *targetFrame, const char *targetShape,
                                        const char *aberration, double stepSize,
                                        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);
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

/**
 * Get celestial body information from his id
 * @param bodyId
 * @return
 */
MODULE_API IO::Astrodynamics::API::DTO::CelestialBodyDTO GetCelestialBodyInfoProxy(int bodyId);

/**
 * Get the transformation from a frame to another frame at given epoch
 * @param fromFrame
 * @param toFrame
 * @return
 */
MODULE_API IO::Astrodynamics::API::DTO::FrameTransformationDTO TransformFrameProxy(const char *fromFrame, const char *toFrame, double epoch);

/**
 * Convert Two line elements to state vector
 * @param L1
 * @param L2
 * @param epoch
 * @return
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ConvertTLEToStateVectorProxy(const char *L1, const char *L2, const char *L3, double epoch);

/**
 * Convert conic orbital elements to state vector
 * @param conicOrbitalElementsDto
 * @return
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ConvertConicElementsToStateVectorProxy(IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElementsDto);

/**
 * Convert equinoctial elements to state vector
 * @param equinoctialElementsDto
 * @return
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ConvertEquinoctialElementsToStateVectorProxy(IO::Astrodynamics::API::DTO::EquinoctialElementsDTO equinoctialElementsDto);

/**
 * Convert stateVector to right ascension and declination
 * @param stateVectorDto
 * @return
 */
MODULE_API IO::Astrodynamics::API::DTO::RaDecDTO ConvertStateVectorToEquatorialCoordinatesProxy(IO::Astrodynamics::API::DTO::StateVectorDTO stateVectorDto);
MODULE_API IO::Astrodynamics::API::DTO::TLEElementsDTO GetTLEElementsProxy(const char *L1, const char *L2, const char *L3);
#ifdef __cplusplus
}
#endif
#pragma endregion Proxy

#ifndef PROXY_H
#define PROXY_H
#define ERRORMSGLENGTH 1024

static constexpr const int lenout = 33;

char *HandleError();

void ActivateErrorManagement();

#endif
