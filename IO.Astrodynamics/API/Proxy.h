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
 * Get the last error message from a failed proxy call
 * @return Error message string, or empty string if no error; every proxy that reports through it clears it when it starts
 * (#714), so the message is that of the last such call
 */
MODULE_API const char *GetLastErrorProxy();

/**
 * Get the spice version
 * @return version
 */
MODULE_API const char *GetSpiceVersionProxy();

/**
 * Write ephemeris into binary file (spk)
 * @param filePath Path to the binary file
 * @param objectId ID of the object
 * @param sv Array of state vectors
 * @param size Size of the state vector array
 * @return true if successful, false otherwise, the message in GetLastErrorProxy: fewer than three states, several frames,
 * a centre that is not a celestial body, a path the file system rejects (#706: these threw a C++ exception across P/Invoke)
 */
MODULE_API bool
WriteEphemerisProxy(const char *filePath, int objectId, IO::Astrodynamics::API::DTO::StateVectorDTO sv[100000],
                    unsigned int size);

/**
 * Write orientation into binary file (ck)
 * @param filePath Path to the binary file
 * @param objectId ID of the object
 * @param so Array of state orientations
 * @param size Size of the state orientation array
 * @return true if successful, false otherwise, the message in GetLastErrorProxy: no record, several frames, a path the file
 * system rejects (#706: these threw a C++ exception across P/Invoke, or read an empty array)
 */
MODULE_API bool WriteOrientationProxy(const char *filePath, int objectId,
                                      IO::Astrodynamics::API::DTO::StateOrientationDTO so[100000], unsigned int size);

/**
 * Read object ephemeris
 * @param searchWindow Time window for the search
 * @param observerId ID of the observer
 * @param targetId ID of the target
 * @param frame Reference frame
 * @param aberration Aberration correction
 * @param stepSize Step size for the search
 * @param stateVectors Array to store the state vectors
 * @return true if successful, false otherwise (call GetLastErrorProxy for details)
 */
MODULE_API bool
ReadEphemerisProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId, int targetId, const char *frame,
                   const char *aberration, double stepSize,
                   IO::Astrodynamics::API::DTO::StateVectorDTO stateVectors[10000]);

/**
 * Read ephemeris at a given epoch
 * @param epoch Epoch time
 * @param observerId ID of the observer
 * @param targetId ID of the target
 * @param frame Reference frame
 * @param aberration Aberration correction
 * @return State vector at the given epoch
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ReadEphemerisAtGivenEpochProxy(
        double epoch, int observerId, int targetId, const char *frame, const char *aberration);

/**
 * Read spacecraft orientation
 * @param searchWindow Time window for the search
 * @param spacecraftId ID of the spacecraft
 * @param tolerance Tolerance for the search
 * @param frame Reference frame
 * @param stepSize Step size for the search
 * @param so Array to store the state orientations
 * @return true if successful, false otherwise (call GetLastErrorProxy for details)
 */
MODULE_API bool
ReadOrientationProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int spacecraftId, double tolerance,
                     const char *frame, double stepSize,
                     IO::Astrodynamics::API::DTO::StateOrientationDTO so[10000]);

/**
 * Evaluate launch windows
 * @param launchDto Launch data transfer object
 * @return true if successful, false otherwise (call GetLastErrorProxy for details)
 */
MODULE_API bool LaunchProxy(IO::Astrodynamics::API::DTO::LaunchDTO &launchDto);

/**
 * Load kernels
 * @param path Directory path to the kernels
 * @return true if successful, false otherwise, with SPICE's message or that of a path the file system rejects in
 * GetLastErrorProxy (#706)
 */
MODULE_API bool LoadKernelsProxy(const char *path);

/**
 * Unload kernels
 * @param path Directory path to the kernels
 * @return true if successful, false otherwise, with SPICE's message or that of a path the file system rejects in
 * GetLastErrorProxy (#706)
 */
MODULE_API bool UnloadKernelsProxy(const char *path);

/**
 * Unload every load of a file a writer replaces: each name SPICE holds that resolves through it (KernelsLoader::
 * ResolvesThrough: the same name, or an absolute name whose symbolic links reach it), after the deleted binary kernels.
 * The managed writer of SPKs of the .NET layer unloads this way the file it replaces, as the native writers do (review of
 * #706: KEEPER's list, which only native code reads, is needed to find every load)
 * @param path Full path of the file
 * @return true if successful, false otherwise, with SPICE's message in GetLastErrorProxy
 */
MODULE_API bool UnloadEveryLoadProxy(const char *path);

/**
 * Whether a kernel name resolves through a file a writer replaces (KernelsLoader::ResolvesThrough), for the registry of
 * the .NET layer, which follows the names the native writers unload (review of #706); no SPICE call
 * @param name A kernel name, as the registry holds it
 * @param path Full path of the file the writer replaces
 * @return true when the name is the path, or an absolute name whose symbolic links, followed one at a time, reach its entry
 */
MODULE_API bool KernelNameResolvesThroughProxy(const char *name, const char *path);

/**
 * Get the name SPICE gives to a frame id (frmnam_c), from its built-in frames and the loaded frame kernels
 * @param frameId Frame id
 * @param name Buffer of 33 characters that receives the name, empty when SPICE knows no frame of that id
 * @return true when SPICE knows a frame of that id, false otherwise (call GetLastErrorProxy for details)
 */
MODULE_API bool GetFrameNameProxy(int frameId, char name[33]);

/**
 * Get the identifier, the centre and the class SPICE gives to a frame name (namfrm_c, frinfo_c), from its built-in frames
 * and the loaded frame kernels: the centre of a non-inertial output frame sets the epoch of its orientation under a light
 * time correction, as spkezr_c evaluates it (#593)
 * @param frameName Frame name
 * @param frameId Receives the frame id, 0 when SPICE knows no frame of that name
 * @param centerId Receives the NAIF id of the frame's centre, 0 when SPICE knows no frame of that name
 * @param frameClass Receives the class of the frame (1 inertial, 2 PCK, 3 CK, 4 TK, 5 dynamic, 6 switch), 0 when SPICE knows
 * no frame of that name
 * @return true when SPICE knows a frame of that name, false otherwise (call GetLastErrorProxy for details)
 */
MODULE_API bool GetFrameInfoProxy(const char *frameName, int *frameId, int *centerId, int *frameClass);

/**
 * Convert seconds from J2000 to formatted string (TDB)
 * @param secondsFromJ2000 Seconds from J2000
 * @return Formatted string
 */
MODULE_API const char *TDBToStringProxy(double secondsFromJ2000);

/**
 * Convert seconds from J2000 to formatted string (UTC)
 * @param secondsFromJ2000 Seconds from J2000
 * @return Formatted string
 */
MODULE_API const char *UTCToStringProxy(double secondsFromJ2000);

/**
 * Find time windows which satisfy distance constraint
 * @param searchWindow Time window for the search
 * @param observerId ID of the observer
 * @param targetId ID of the target
 * @param relationalOperator Relational operator for the constraint
 * @param value Value for the constraint
 * @param aberration Aberration correction
 * @param stepSize Step size for the search
 * @param windows Array to store the time windows
 * @return true if successful, false otherwise (call GetLastErrorProxy for details); the windows are then not written
 */
MODULE_API bool
FindWindowsOnDistanceConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                                     const char *relationalOperator, double value, const char *aberration,
                                     double stepSize, IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows which satisfy occultation constraint
 * @param searchWindow Time window for the search
 * @param observerId ID of the observer
 * @param targetId ID of the target
 * @param targetFrame Reference frame of the target
 * @param targetShape Shape of the target
 * @param frontBodyId ID of the front body
 * @param frontFrame Reference frame of the front body
 * @param frontShape Shape of the front body
 * @param occultationType Type of occultation
 * @param aberration Aberration correction
 * @param stepSize Step size for the search
 * @param windows Array to store the time windows
 * @return true if successful, false otherwise (call GetLastErrorProxy for details); the windows are then not written
 */
MODULE_API bool FindWindowsOnOccultationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow,
                                                        int observerId,
                                                        int targetId, const char *targetFrame,
                                                        const char *targetShape,
                                                        int frontBodyId, const char *frontFrame, const char *frontShape,
                                                        const char *occultationType,
                                                        const char *aberration, double stepSize,
                                                        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows which satisfy coordinate constraint
 * @param searchWindow Time window for the search
 * @param observerId ID of the observer
 * @param targetId ID of the target
 * @param frame Reference frame
 * @param coordinateSystem Coordinate system
 * @param coordinate Coordinate to be constrained
 * @param relationalOperator Relational operator for the constraint
 * @param value Value for the constraint
 * @param adjustValue Adjustment value for the constraint
 * @param aberration Aberration correction
 * @param stepSize Step size for the search
 * @param windows Array to store the time windows
 * @return true if successful, false otherwise (call GetLastErrorProxy for details); the windows are then not written
 */
MODULE_API bool FindWindowsOnCoordinateConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow,
                                                       int observerId,
                                                       int targetId, const char *frame, const char *coordinateSystem,
                                                       const char *coordinate, const char *relationalOperator,
                                                       double value, double adjustValue, const char *aberration,
                                                       double stepSize,
                                                       IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows which satisfy illumination constraint
 * @param searchWindow Time window for the search
 * @param observerId ID of the observer
 * @param illuminationSource Source of illumination
 * @param targetBody ID of the target body
 * @param fixedFrame Fixed reference frame
 * @param geodetic Geodetic coordinates
 * @param illuminationType Type of illumination
 * @param relationalOperator Relational operator for the constraint
 * @param value Value for the constraint
 * @param adjustValue Adjustment value for the constraint
 * @param aberration Aberration correction
 * @param stepSize Step size for the search
 * @param method Method for the search
 * @param windows Array to store the time windows
 * @return true if successful, false otherwise (call GetLastErrorProxy for details); the windows are then not written
 */
MODULE_API bool FindWindowsOnIlluminationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow,
                                                         int observerId,
                                                         const char *illuminationSource, int targetBody,
                                                         const char *fixedFrame,
                                                         IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic,
                                                         const char *illuminationType,
                                                         const char *relationalOperator, double value,
                                                         double adjustValue,
                                                         const char *aberration, double stepSize, const char *method,
                                                         IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

/**
 * Find time windows which satisfy in field of view constraint
 * @param searchWindow Time window for the search
 * @param observerId ID of the observer
 * @param instrumentId ID of the instrument
 * @param targetId ID of the target
 * @param targetFrame Reference frame of the target
 * @param targetShape Shape of the target
 * @param aberration Aberration correction
 * @param stepSize Step size for the search
 * @param windows Array to store the time windows
 * @return true if successful, false otherwise (call GetLastErrorProxy for details); the windows are then not written
 */
MODULE_API bool
FindWindowsInFieldOfViewConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                        int instrumentId,
                                        int targetId, const char *targetFrame, const char *targetShape,
                                        const char *aberration, double stepSize,
                                        IO::Astrodynamics::API::DTO::WindowDTO windows[1000]);

/**
 * Convert elapsed seconds from J2000 to UTC
 * @param tdb Time in TDB
 * @return Time in UTC
 */
MODULE_API double ConvertTDBToUTCProxy(double tdb);

/**
 * Convert elapsed seconds from J2000 to TDB
 * @param utc Time in UTC
 * @return Time in TDB
 */
MODULE_API double ConvertUTCToTDBProxy(double utc);

/**
 * Get celestial body information from its ID
 * @param bodyId ID of the celestial body
 * @return Celestial body information
 */
MODULE_API IO::Astrodynamics::API::DTO::CelestialBodyDTO GetCelestialBodyInfoProxy(int bodyId);

/**
 * Get the transformation from one frame to another at a given epoch
 * @param fromFrame Source reference frame
 * @param toFrame Target reference frame
 * @param epoch Epoch time
 * @return Frame transformation information; a NaN rotation and angular velocity, SPICE's message in GetLastErrorProxy, when
 * SPICE gives none (#714: an unknown frame, frame data that do not cover the epoch)
 */
MODULE_API IO::Astrodynamics::API::DTO::FrameTransformationDTO TransformFrameProxy(
        const char *fromFrame, const char *toFrame, double epoch);

/**
 * Convert Two Line Elements (TLE) to state vector
 * @param L1 Line 1 of TLE
 * @param L2 Line 2 of TLE
 * @param L3 Line 3 of TLE
 * @param epoch Epoch time
 * @return State vector
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ConvertTLEToStateVectorProxy(
        const char *L1, const char *L2, const char *L3, double epoch);

/**
 * Convert conic orbital elements to state vector
 * @param conicOrbitalElementsDto Conic orbital elements
 * @return State vector
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ConvertConicElementsToStateVectorProxy(
        IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElementsDto);

/**
 * Convert equinoctial elements to state vector at epoch
 * @param equinoctialElementsDto
 * @param epoch
 * @return
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ConvertConicElementsToStateVectorAtEpochProxy(
        IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO equinoctialElementsDto, double epoch, double gm);

/**
 * Convert state vector to conic orbital elements
 * @param stateVector State vector
 * @param mu Gravitational parameter
 * @return Conic orbital elements; NaN in every element (the epoch, centre and frame as given), SPICE's message in
 * GetLastErrorProxy, when oscltx_c fails (#714)
 */
MODULE_API IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO ConvertStateVectorToConicOrbitalElementProxy(
        IO::Astrodynamics::API::DTO::StateVectorDTO stateVector, double mu);

/**
 * Convert equinoctial elements to state vector
 * @param equinoctialElementsDto Equinoctial elements
 * @return State vector
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO ConvertEquinoctialElementsToStateVectorProxy(
        IO::Astrodynamics::API::DTO::EquinoctialElementsDTO equinoctialElementsDto);


/**
 * Convert state vector to right ascension and declination
 * @param stateVectorDto State vector
 * @return Right ascension and declination
 */
MODULE_API IO::Astrodynamics::API::DTO::RaDecDTO ConvertStateVectorToEquatorialCoordinatesProxy(
        IO::Astrodynamics::API::DTO::StateVectorDTO stateVectorDto);

/**
 * Propagate the state vector of a two-body system.
 * @param stateVector state vector elements of the two-body system
 * @return Propagated state vector; a NaN state at the epoch plus dt (the centre and frame as given), SPICE's message in
 * GetLastErrorProxy, when prop2b_c fails (#714)
 */
MODULE_API IO::Astrodynamics::API::DTO::StateVectorDTO Propagate2BodiesProxy(
        IO::Astrodynamics::API::DTO::StateVectorDTO stateVector, double gm, double dt);


/**
 * Get TLE elements
 * @param L1 Line 1 of TLE
 * @param L2 Line 2 of TLE
 * @param L3 Line 3 of TLE
 * @return TLE elements
 */
MODULE_API IO::Astrodynamics::API::DTO::TLEElementsDTO GetTLEElementsProxy(
        const char *L1, const char *L2, const char *L3);

/**
 * Clear kernel pool
 */
MODULE_API void KClearProxy();
#ifdef __cplusplus
}
#endif
#pragma endregion Proxy

#ifndef PROXY_H
#define PROXY_H
#define ERRORMSGLENGTH 1024

static constexpr const int lenout = 33;

/**
 * Handle error and return error message
 * @return Error message
 */
char *HandleError();

/**
 * Activate error management
 */
void ActivateErrorManagement();

#endif
