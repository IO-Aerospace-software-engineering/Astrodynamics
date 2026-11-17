/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <iostream>
#include <filesystem>
#include <cstring>
#include <string>


#include <Proxy.h>
#include <Converters.cpp>
#include <InertialFrames.h>
#include <KernelsLoader.h>
#include <TLE.h>
#include <EquinoctialElements.h>
#include <SDKException.h>
#include "InvalidArgumentException.h"
#include "OrientationKernel.h"
#include "EphemerisKernel.h"
#include "SpacecraftClockKernel.h"
#include "LaunchSite.h"
#include "Launch.h"

#pragma region Proxy

static thread_local char lastError[2048] = "";

const char *GetLastErrorProxy()
{
    return lastError;
}

const char *GetSpiceVersionProxy()
{
    ActivateErrorManagement();
    const char *version;
    version = tkvrsn_c("TOOLKIT");
    if (failed_c())
    {
        HandleError();
    }
    return strdup(version);
}

namespace
{
    // The failure of a native writer: the message of its writing, taken (and SPICE reset) before its kernel object is
    // destroyed, then that of the destruction, whose unload of the written file opens the other loaded kernels of its kind
    // again (fourth round of #668). Copied into lastError; true when there is none. The kernel loaders report the same way,
    // the message of a C++ exception then SPICE's (#706: they reset SPICE without keeping its message, so that the .NET layer
    // could not name the cause)
    //
    // A SPICE error is reported by its long message, or by its short one when it has none (#714: prop2b.c signals
    // SPICE(NONPOSITIVEMASS), SPICE(ZEROPOSITION), SPICE(ZEROVELOCITY) and SPICE(NONCONICMOTION) with no setmsg_, so that the
    // message was empty and the failed call reported as a success)
    bool ReportNativeErrors(std::string writing)
    {
        std::string message = std::move(writing);
        if (failed_c())
        {
            SpiceChar shortMessage[SPICE_ERROR_SMSGLN]{};
            getmsg_c("SHORT", SPICE_ERROR_SMSGLN, shortMessage);
            std::string spice = HandleError();
            if (spice.empty()) spice = shortMessage;
            if (!message.empty()) message += " ";
            message += spice;
        }
        if (message.empty())
        {
            return true;
        }
        std::strncpy(lastError, message.c_str(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        return false;
    }

    // The message of the call that starts (#714): lastError was written only on failure, so that GetLastErrorProxy returned
    // the message of an earlier call, about another body, after a call that succeeded or that failed without a message of
    // its own. Called first by every proxy that reports through lastError
    void ClearLastError()
    {
        lastError[0] = '\0';
    }

    // A proxy that failed with a C++ exception: its message, then SPICE's, SPICE reset (#711, #712). Always false
    bool ReportException(const std::exception &e)
    {
        const std::string what = e.what();
        return ReportNativeErrors(what.empty() ? std::string("C++ exception in the native layer") : what);
    }

    // The NaN state of a failed conversion (#711), as ReadEphemerisAtGivenEpochProxy's (#454): the epoch, the centre and the
    // frame as given
    IO::Astrodynamics::API::DTO::StateVectorDTO FailedState(double epoch, int centerOfMotionId, const char *frame)
    {
        IO::Astrodynamics::API::DTO::StateVectorDTO sv;
        sv.epoch = epoch;
        sv.centerOfMotionId = centerOfMotionId;
        sv.SetFrame(frame);
        sv.position = sv.velocity = {std::nan(""), std::nan(""), std::nan("")};
        return sv;
    }

    // Appends SPICE's message to the writing's, and resets SPICE, so that a kernel object is destroyed on a clean SPICE
    // state whether its writing failed through SPICE or through a C++ exception
    void TakeSpiceError(std::string &writing)
    {
        if (failed_c())
        {
            if (!writing.empty()) writing += " ";
            writing += HandleError();
        }
    }
}

bool LaunchProxy(IO::Astrodynamics::API::DTO::LaunchDTO &launchDto)
{
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        auto celestialBody = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(launchDto.recoverySite.bodyId);
        IO::Astrodynamics::Sites::LaunchSite ls(launchDto.launchSite.id, launchDto.launchSite.name,
                                                ToPlanetodetic(launchDto.launchSite.coordinates),
                                                std::make_shared<IO::Astrodynamics::Body::CelestialBody>(
                                                        launchDto.launchSite.bodyId),
                                                launchDto.launchSite.directoryPath);
        IO::Astrodynamics::Sites::LaunchSite rs(launchDto.recoverySite.id, launchDto.recoverySite.name,
                                                ToPlanetodetic(launchDto.recoverySite.coordinates),
                                                std::make_shared<IO::Astrodynamics::Body::CelestialBody>(
                                                        launchDto.recoverySite.bodyId),
                                                launchDto.launchSite.directoryPath);
        IO::Astrodynamics::OrbitalParameters::StateVector sv(celestialBody, ToVector3D(launchDto.targetOrbit.position),
                                                             ToVector3D(launchDto.targetOrbit.velocity),
                                                             IO::Astrodynamics::Time::TDB(
                                                                     std::chrono::duration<double>(
                                                                             launchDto.targetOrbit.epoch)),
                                                             IO::Astrodynamics::Frames::Frames(
                                                                     launchDto.targetOrbit.inertialFrame));
        IO::Astrodynamics::Maneuvers::Launch launch(ls, rs, launchDto.launchByDay, sv);
        auto tdbWindow = ToTDBWindow(launchDto.window);
        auto res = launch.GetLaunchWindows(
                IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>(tdbWindow.GetStartDate().ToUTC(),
                                                                              tdbWindow.GetEndDate().ToUTC()));
        for (size_t i = 0; i < res.size(); ++i)
        {
            launchDto.windows[i] = ToWindowDTO(res[i].GetWindow());
            launchDto.inertialAzimuth[i] = res[i].GetInertialAzimuth();
            launchDto.nonInertialAzimuth[i] = res[i].GetNonInertialAzimuth();
            launchDto.inertialInsertionVelocity[i] = res[i].GetInertialInsertionVelocity();
            launchDto.nonInertialInsertionVelocity[i] = res[i].GetNonInertialInsertionVelocity();
        }
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }
        return true;
    }
    catch (const std::exception &e)
    {
        // The exception's message, then SPICE's pending one, SPICE reset (#712: before, SPICE stayed failed)
        return ReportException(e);
    }
}

// The kernel object lives in an inner scope: its destructor unloads the written file (Kernel::~Kernel), and that unload_c
// opens the other loaded SPKs again (unload_c --> UNLOAD --> SPKLEF), which fails on a deleted SPK SPICE holds under a
// relative name, left to KEEPER by KernelsLoader. Before the fourth round of #668 the destructor ran after the return, so
// that failure was left pending into the next proxy while this one returned true. The writing's error is taken first, so
// that the destructor unloads on a clean SPICE state, as it did before
//
// No C++ exception crosses P/Invoke (#706): the writer's own rules (EphemerisKernel::WriteData: three states or more, in one
// frame), the centre of motion (CelestialBody) and the throwing std::filesystem calls of the kernel object (a symbolic-link
// loop, a removal Windows refuses) threw out of the proxy, which ended the host process (std::terminate). The writing is
// caught inside the kernel object's scope, so that its destructor still unloads on a clean SPICE state, and the construction
// of the kernel object outside it; each reports its message through lastError
bool WriteEphemerisProxy(const char *filePath, int objectId, IO::Astrodynamics::API::DTO::StateVectorDTO *sv,
                         unsigned int size)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string writing;
    try
    {
        IO::Astrodynamics::Kernels::EphemerisKernel kernel(filePath, objectId);
        try
        {
            std::vector<IO::Astrodynamics::OrbitalParameters::StateVector> states;
            states.reserve(size);
            std::map<int, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody>> celestialBodies;

            for (unsigned int i = 0; i < size; ++i)
            {
                if (celestialBodies.find(sv[i].centerOfMotionId) == celestialBodies.end())
                {
                    celestialBodies[sv[i].centerOfMotionId] = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(
                            sv[i].centerOfMotionId);
                }
                states.emplace_back(celestialBodies[sv[i].centerOfMotionId], ToVector3D(sv[i].position),
                                    ToVector3D(sv[i].velocity),
                                    IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(sv[i].epoch)),
                                    IO::Astrodynamics::Frames::Frames(sv[i].inertialFrame));
            }

            kernel.WriteData(states);
        }
        catch (const std::exception &e)
        {
            writing = e.what();
        }
        TakeSpiceError(writing);
    }
    catch (const std::exception &e)
    {
        writing = e.what();
    }
    return ReportNativeErrors(std::move(writing));
}

// The CK analog of WriteEphemerisProxy: the destructor's unload of the written CK opens the other loaded CKs again
// (unload_c --> UNLOAD --> CKLPF); no C++ exception crosses P/Invoke either (#706)
bool WriteOrientationProxy(const char *filePath, int objectId, IO::Astrodynamics::API::DTO::StateOrientationDTO *so,
                           unsigned int size)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string writing;
    try
    {
        IO::Astrodynamics::Kernels::OrientationKernel kernel(filePath, objectId, objectId * 1000);
        try
        {
            std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> states;
            states.emplace_back();

            states.back().reserve(size);

            for (unsigned int i = 0; i < size; ++i)
            {
                states.back().emplace_back(ToQuaternion(so[i].orientation), ToVector3D(so[i].angularVelocity),
                                           IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(so[i].epoch)),
                                           IO::Astrodynamics::Frames::Frames(so[i].frame));
            }
            kernel.WriteOrientations(states);
        }
        catch (const std::exception &e)
        {
            writing = e.what();
        }
        TakeSpiceError(writing);
    }
    catch (const std::exception &e)
    {
        writing = e.what();
    }
    return ReportNativeErrors(std::move(writing));
}

bool ReadOrientationProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int spacecraftId, double tolerance,
                          const char *frame,
                          double stepSize, IO::Astrodynamics::API::DTO::StateOrientationDTO *so)
{
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        if ((searchWindow.end - searchWindow.start) / stepSize > 10000)
        {
            std::strncpy(lastError,
                    "Step size to small or search window to large. The number of State orientation must be lower than 10000",
                    sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }
        //Build platform id
        SpiceInt id = spacecraftId * 1000;

        // Convert tolerance from TDB seconds to encoded spacecraft clock ticks.
        // ckgpav_c requires tolerance in ticks, not seconds.
        // Computing once at window start is valid for linear (type 1) SCLK.
        SpiceDouble sclk0{}, sclkPlusTol{};
        sce2c_c(spacecraftId, searchWindow.start, &sclk0);
        sce2c_c(spacecraftId, searchWindow.start + tolerance, &sclkPlusTol);
        // SPICE's error of a missing clock (#712: before, reset by the fallback below and lost)
        if (failed_c())
        {
            return ReportNativeErrors("");
        }
        SpiceDouble toleranceInTicks = std::abs(sclkPlusTol - sclk0);

        double epoch = searchWindow.start;
        int idx{0};
        while (epoch <= searchWindow.end && idx < 10000)
        {
            //Get encoded clock
            SpiceDouble sclk = IO::Astrodynamics::Kernels::SpacecraftClockKernel::ConvertToEncodedClock(spacecraftId,
                                                                                                        IO::Astrodynamics::Time::TDB(
                                                                                                                std::chrono::duration<double>(
                                                                                                                        epoch)));
            if (failed_c())
            {
                return ReportNativeErrors("");
            }

            SpiceDouble cmat[3][3]{};
            SpiceDouble av[3]{};
            SpiceDouble clkout{};
            SpiceBoolean found{SPICEFALSE};

            //Get orientation and angular velocity
            ckgpav_c(id, sclk, toleranceInTicks, frame, cmat, av, &clkout, &found);
            if (failed_c() || !found)
            {
                // ckgpav_c skips a segment without angular velocity (a type-1 CK, a type-2 or type-3 CK written without
                // rates) and returns found = false with no error (ckgpav_c.c, Restrictions 1), so the pointing is read by
                // ckgp_c, with a zero angular velocity (#714: before, only a failed ckgpav_c fell back, and such a CK gave "No
                // orientation found"). A failed ckgpav_c (no CK loaded, an unknown frame) falls back too, and ckgp_c's error
                // is then reported
                if (failed_c())
                {
                    reset_c();
                }
                av[0] = av[1] = av[2] = 0.0;
                found = SPICEFALSE;
                ckgp_c(id, sclk, toleranceInTicks, frame, cmat, &clkout, &found);
                // The fallback fails too (no CK loaded, an unknown frame): SPICE's message, SPICE reset (#712: before, "No
                // orientation found" with the error left pending for the next call)
                if (failed_c())
                {
                    return ReportNativeErrors("");
                }
            }

            if (!found)
            {
                std::strncpy(lastError, "No orientation found", sizeof(lastError) - 1);
                lastError[sizeof(lastError) - 1] = '\0';
                return false;
            }

            // Use Matrix(double[3][3]) constructor directly — no heap allocation needed
            IO::Astrodynamics::Math::Matrix mat(cmat);
            IO::Astrodynamics::Math::Quaternion q(mat);

            double correctedEpoch{};
            sct2e_c(spacecraftId, clkout, &correctedEpoch);
            if (failed_c())
            {
                return ReportNativeErrors("");
            }
            so[idx].epoch = correctedEpoch;
            so[idx].SetFrame(frame);
            so[idx].orientation = ToQuaternionDTO(q);
            so[idx].angularVelocity.x = av[0];
            so[idx].angularVelocity.y = av[1];
            so[idx].angularVelocity.z = av[2];

            epoch += stepSize;
            idx++;
        }
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }
        return true;
    }
    catch (const std::exception &e)
    {
        // The exception's message, then SPICE's pending one, SPICE reset (#712: before, SPICE stayed failed)
        return ReportException(e);
    }
}

// No C++ exception crosses P/Invoke (#706): the throwing std::filesystem calls of KernelsLoader (is_regular_file of a
// symbolic-link loop, a directory_iterator that fails) ended the host process. SPICE's message of a failed load or unload is
// kept for GetLastErrorProxy (#706; before, it was reset and lost)
bool LoadKernelsProxy(const char *path)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    try
    {
        IO::Astrodynamics::Kernels::KernelsLoader::Load(path);
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    return ReportNativeErrors(std::move(message));
}

bool UnloadKernelsProxy(const char *path)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    try
    {
        IO::Astrodynamics::Kernels::KernelsLoader::Unload(path);
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    return ReportNativeErrors(std::move(message));
}

bool UnloadEveryLoadProxy(const char *path)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    try
    {
        IO::Astrodynamics::Kernels::KernelsLoader::UnloadEveryLoad(path);
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    return ReportNativeErrors(std::move(message));
}

bool KernelNameResolvesThroughProxy(const char *name, const char *path)
{
    try
    {
        return IO::Astrodynamics::Kernels::KernelsLoader::ResolvesThrough(name, path);
    }
    catch (const std::exception &)
    {
        return false;
    }
}

bool GetFrameNameProxy(int frameId, char name[33])
{
    ActivateErrorManagement();
    ClearLastError();
    name[0] = '\0';
    frmnam_c(frameId, lenout, name);
    if (failed_c())
    {
        std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        name[0] = '\0';
        return false;
    }

    if (name[0] == '\0')
    {
        std::snprintf(lastError, sizeof(lastError), "SPICE knows no frame of id %d", frameId);
        return false;
    }
    return true;
}

bool GetFrameInfoProxy(const char *frameName, int *frameId, int *centerId, int *frameClass)
{
    ActivateErrorManagement();
    ClearLastError();
    *frameId = 0;
    *centerId = 0;
    *frameClass = 0;
    SpiceInt id = 0;
    namfrm_c(frameName, &id);
    SpiceInt center = 0, frclss = 0, clssid = 0;
    SpiceBoolean found = SPICEFALSE;
    if (!failed_c() && id != 0)
    {
        frinfo_c(id, &center, &frclss, &clssid, &found);
    }

    if (failed_c())
    {
        std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        return false;
    }

    if (id == 0 || !found)
    {
        std::snprintf(lastError, sizeof(lastError), "SPICE knows no frame named %s", frameName);
        return false;
    }

    *frameId = static_cast<int>(id);
    *centerId = static_cast<int>(center);
    *frameClass = static_cast<int>(frclss);
    return true;
}

const char *TDBToStringProxy(double secondsFromJ2000)
{
    ActivateErrorManagement();
    IO::Astrodynamics::Time::TDB tdb((std::chrono::duration<double>(secondsFromJ2000)));
    std::string str = tdb.ToString();
    if (failed_c())
    {
        return strdup(HandleError());
    }
    return strdup(str.c_str());
}

const char *UTCToStringProxy(double secondsFromJ2000)
{
    ActivateErrorManagement();
    IO::Astrodynamics::Time::UTC utc((std::chrono::duration<double>(secondsFromJ2000)));
    std::string str = utc.ToString();
    if (failed_c())
    {
        return strdup(HandleError());
    }
    return strdup(str.c_str());
}

static IO::Astrodynamics::API::DTO::StateVectorDTO ReadStateAt(double epoch, int observerId, int targetId, const char *frame,
                                                              const char *aberration);

bool ReadEphemerisProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                        const char *frame, const char *aberration, double stepSize,
                        IO::Astrodynamics::API::DTO::StateVectorDTO *stateVectors)
{
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        if ((searchWindow.end - searchWindow.start) / stepSize > 10000)
        {
            std::strncpy(lastError,
                    "Step size to small or search window to large. The number of State vector must be lower than 10000",
                    sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }
        int idx = 0;
        double epoch = searchWindow.start;
        while (epoch <= searchWindow.end && idx < 10000)
        {
            stateVectors[idx] = ReadStateAt(epoch, observerId, targetId, frame, aberration);
            epoch += stepSize;
            idx++;
        }
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }
        return true;
    }
    catch (const std::exception &e)
    {
        // The exception's message, then SPICE's pending one, SPICE reset (#712: before, SPICE stayed failed)
        return ReportException(e);
    }
}

bool FindWindowsOnDistanceConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                          int targetId,
                                          const char *relationalOperator, double value, const char *aberration,
                                          double stepSize, IO::Astrodynamics::API::DTO::WindowDTO windows[1000])
{
    // An error SPICE signals is the caller's, as for ReadEphemerisProxy: the windows are not written (fourth round of #668;
    // before, the error was printed and dropped and the search returned no window)
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        auto relationalOpe = IO::Astrodynamics::Constraints::RelationalOperator::ToRelationalOperator(relationalOperator);
        auto abe = IO::Astrodynamics::Aberrations::ToEnum(aberration);

        auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(
                ToTDBWindow(searchWindow),
                observerId, targetId,
                relationalOpe, value, abe,
                IO::Astrodynamics::Time::TimeSpan(stepSize));
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }

        for (size_t i = 0; i < res.size(); ++i)
        {
            if (i >= 1000) break;
            windows[i] = ToWindowDTO(res[i]);
        }
        return true;
    }
    catch (const std::exception &e)
    {
        if (failed_c())
        {
            HandleError();
        }
        std::strncpy(lastError, e.what(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        return false;
    }
}

bool
FindWindowsOnOccultationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                        int targetId,
                                        const char *targetFrame, const char *targetShape, int frontBodyId,
                                        const char *frontFrame, const char *frontShape, const char *occultationType,
                                        const char *aberration, double stepSize,
                                        IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    // An error SPICE signals is the caller's, as for ReadEphemerisProxy: the windows are not written (fourth round of #668;
    // before, the error was printed and dropped and the search returned no window)
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        auto abe = IO::Astrodynamics::Aberrations::ToEnum(aberration);
        auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnOccultationConstraint(
                ToTDBWindow(searchWindow),
                observerId, targetId,
                targetFrame, targetShape,
                frontBodyId,
                frontFrame, frontShape,
                IO::Astrodynamics::OccultationType::ToOccultationType(
                        occultationType), abe,
                IO::Astrodynamics::Time::TimeSpan(
                        stepSize));
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }

        for (size_t i = 0; i < res.size(); ++i)
        {
            if (i >= 1000) break;
            windows[i] = ToWindowDTO(res[i]);
        }
        return true;
    }
    catch (const std::exception &e)
    {
        if (failed_c())
        {
            HandleError();
        }
        std::strncpy(lastError, e.what(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        return false;
    }
}

bool
FindWindowsOnCoordinateConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                       int targetId,
                                       const char *frame, const char *coordinateSystem,
                                       const char *coordinate,
                                       const char *relationalOperator, double value, double adjustValue,
                                       const char *aberration, double stepSize,
                                       IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    // An error SPICE signals is the caller's, as for ReadEphemerisProxy: the windows are not written (fourth round of #668;
    // before, the error was printed and dropped and the search returned no window)
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        auto abe = IO::Astrodynamics::Aberrations::ToEnum(aberration);
        auto systemType = IO::Astrodynamics::CoordinateSystem::ToCoordinateSystemType(coordinateSystem);
        auto coordinateType = IO::Astrodynamics::Coordinate::ToCoordinateType(coordinate);
        auto relationalOpe = IO::Astrodynamics::Constraints::RelationalOperator::ToRelationalOperator(relationalOperator);
        auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnCoordinateConstraint(
                ToTDBWindow(searchWindow),
                observerId, targetId, frame,
                systemType, coordinateType,
                relationalOpe, value,
                adjustValue, abe,
                IO::Astrodynamics::Time::TimeSpan(
                        stepSize));
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }

        for (size_t i = 0; i < res.size(); ++i)
        {
            if (i >= 1000) break;
            windows[i] = ToWindowDTO(res[i]);
        }
        return true;
    }
    catch (const std::exception &e)
    {
        if (failed_c())
        {
            HandleError();
        }
        std::strncpy(lastError, e.what(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        return false;
    }
}

bool FindWindowsOnIlluminationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                              const char *illuminationSource, int targetBody, const char *fixedFrame,
                                              IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic,
                                              const char *illuminationType,
                                              const char *relationalOperator, double value,
                                              double adjustValue,
                                              const char *aberration, double stepSize, const char *method,
                                              IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    // An error SPICE signals is the caller's, as for ReadEphemerisProxy: the windows are not written (fourth round of #668;
    // before, the error was printed and dropped and the search returned no window)
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        double coordinates[3] = {geodetic.latitude, geodetic.longitude, geodetic.altitude};

        IO::Astrodynamics::Body::CelestialBody body(targetBody);
        SpiceDouble bodyFixedLocation[3];
        georec_c(geodetic.longitude, geodetic.latitude, geodetic.altitude, body.GetRadius().GetX() * 0.001,
                 body.GetFlattening(),
                 bodyFixedLocation);
        auto abe = IO::Astrodynamics::Aberrations::ToEnum(aberration);
        auto illumination = IO::Astrodynamics::IlluminationAngle::ToIlluminationAngleType(illuminationType);
        auto relationalOpe = IO::Astrodynamics::Constraints::RelationalOperator::ToRelationalOperator(relationalOperator);
        auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnIlluminationConstraint(
                ToTDBWindow(searchWindow),
                observerId, illuminationSource,
                targetBody, fixedFrame,
                bodyFixedLocation,
                illumination, relationalOpe,
                value, adjustValue, abe,
                IO::Astrodynamics::Time::TimeSpan(
                        stepSize), method);
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }

        for (size_t i = 0; i < res.size(); ++i)
        {
            if (i >= 1000) break;
            windows[i] = ToWindowDTO(res[i]);
        }
        return true;
    }
    catch (const std::exception &e)
    {
        if (failed_c())
        {
            HandleError();
        }
        std::strncpy(lastError, e.what(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        return false;
    }
}

bool
FindWindowsInFieldOfViewConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                        int instrumentId,
                                        int targetId, const char *targetFrame,
                                        const char *targetShape,
                                        const char *aberration, double stepSize,
                                        IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    // An error SPICE signals is the caller's, as for ReadEphemerisProxy: the windows are not written (fourth round of #668;
    // before, the error was printed and dropped and the search returned no window)
    try
    {
        ActivateErrorManagement();
        ClearLastError();
        auto abe = IO::Astrodynamics::Aberrations::ToEnum(aberration);
        auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(
                ToTDBWindow(searchWindow),
                observerId, instrumentId,
                targetId, targetFrame,
                targetShape,
                abe, IO::Astrodynamics::Time::TimeSpan(
                        stepSize));
        if (failed_c())
        {
            std::strncpy(lastError, HandleError(), sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
            return false;
        }

        for (size_t i = 0; i < res.size(); ++i)
        {
            if (i >= 1000) break;
            windows[i] = ToWindowDTO(res[i]);
        }
        return true;
    }
    catch (const std::exception &e)
    {
        if (failed_c())
        {
            HandleError();
        }
        std::strncpy(lastError, e.what(), sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
        return false;
    }
}

double ConvertTDBToUTCProxy(double tdb)
{
    ActivateErrorManagement();
    double delta{};
    deltet_c(tdb, "et", &delta);
    if (failed_c())
    {
        HandleError();
        return std::numeric_limits<double>::quiet_NaN();
    }
    return tdb - delta;
}

double ConvertUTCToTDBProxy(double utc)
{
    ActivateErrorManagement();
    double delta{};
    deltet_c(utc, "UTC", &delta);
    if (failed_c())
    {
        HandleError();
        return std::numeric_limits<double>::quiet_NaN();
    }
    return utc + delta;
}

// The GM of a body as the kernel pool gives it, m^3/s^2, or 0 when the pool has no BODYnnn_GM; for the solar system
// barycentre, the sum over 1 to 10, the rule of CelestialBody::ReadGM. Read only where bodfnd_c finds it, so that a missing
// GM raises no SPICE error, which would leave SPICE failed for the next reads of the body information (#672).
// CelestialBody::ReadGM is the mass of the native constructors: it gives 0 when the GM is missing and leaves SPICE's error
// signalled, which the proxies that build a celestial body report (#711)
static double ReadDefinedGM(int bodyId)
{
    if (bodyId == 0)
    {
        double solarSystemGM{};
        for (int i = 1; i <= 10; ++i)
        {
            solarSystemGM += ReadDefinedGM(i);
        }
        return solarSystemGM;
    }
    return bodfnd_c(bodyId, "GM") ? IO::Astrodynamics::Body::CelestialBody::ReadGM(bodyId) : 0.0;
}

static IO::Astrodynamics::API::DTO::CelestialBodyDTO ReadCelestialBodyInfo(int bodyId);

// No C++ exception crosses P/Invoke (#711): the search of the centre of motion throws InvalidArgumentException for a named
// identifier outside the celestial categories (a spacecraft such as GALILEO ORBITER, -77); the record is then the one of an
// identifier SPICE does not name (Id -1), the exception's message in lastError
IO::Astrodynamics::API::DTO::CelestialBodyDTO GetCelestialBodyInfoProxy(int bodyId)
{
    ActivateErrorManagement();
    ClearLastError();
    try
    {
        return ReadCelestialBodyInfo(bodyId);
    }
    catch (const std::exception &e)
    {
        ReportException(e);
        return IO::Astrodynamics::API::DTO::CelestialBodyDTO{};
    }
}

static IO::Astrodynamics::API::DTO::CelestialBodyDTO ReadCelestialBodyInfo(int bodyId)
{
    // Zero-initialised: an unnamed identifier returns Id -1 and empty names, a barycentre an empty frame name (#672)
    IO::Astrodynamics::API::DTO::CelestialBodyDTO res{};

    SpiceChar name[32]{};
    SpiceBoolean found{false};
    bodc2n_c(bodyId, 32, name, &found);
    if (found)
    {
        res.Id = bodyId;
        res.SetName(name);
        res.CenterOfMotionId = IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(bodyId);
        res.BarycenterOfMotionId = IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(bodyId);

        SpiceInt dim{0};
        // Search body's radii, 0 when the kernel pool has none (#672)
        if (bodyId >= 10 && bodfnd_c(bodyId, "RADII"))
        {
            SpiceDouble radiiRes[3]{};
            bodvcd_c(bodyId, "RADII", 3, &dim, radiiRes);
            if (dim > 0)
            {
                res.Radii.x = radiiRes[0] * 1000.0;
                if (dim > 1)
                {
                    res.Radii.y = radiiRes[1] * 1000.0;
                    if (dim > 2)
                    {
                        res.Radii.z = radiiRes[2] * 1000.0;
                    }
                }
            }
        }

        // Search CelestialItem's mass, 0 when the kernel pool has none (#672)
        res.GM = ReadDefinedGM(bodyId);

        //Read J Parameters
        res.J2 = IO::Astrodynamics::Body::CelestialBody::ReadJ2(bodyId);
        res.J3 = IO::Astrodynamics::Body::CelestialBody::ReadJ3(bodyId);
        res.J4 = IO::Astrodynamics::Body::CelestialBody::ReadJ4(bodyId);

        // Search frame
        if (!IO::Astrodynamics::Body::CelestialBody::IsBarycenter(bodyId))
        {
            SpiceBoolean frameFound{false};
            SpiceChar frname[lenout]{};
            SpiceInt frcode{};
            cnmfrm_c(name, lenout, &frcode, frname, &frameFound);
            if (frameFound)
            {
                res.SetFrame(frname);
                res.FrameId = frcode;
            }
        }
    }
    if (failed_c())
    {
        HandleError();
    }
    return res;
}

void ActivateErrorManagement()
{
    SpiceChar errorMode[7] = "RETURN";
    erract_c("SET", ERRORMSGLENGTH, errorMode);
}

char *HandleError()
{
    static SpiceChar msg[ERRORMSGLENGTH];
    getmsg_c("LONG", ERRORMSGLENGTH, msg);
    reset_c();
    return msg;
}

// No failure is silent and no C++ exception crosses P/Invoke (#714): a failed sxform_c (SPICE(UNKNOWNFRAME) of a frame SPICE does
// not know, SPICE(FRAMEDATANOTFOUND) outside the coverage of the frame data) gives a NaN rotation and angular velocity with
// SPICE's message in lastError, SPICE reset, before the unwritten matrix reaches the quaternion and xf2rav_c. Before, SPICE
// was checked only after xf2rav_c, then reset without its message, and the zero quaternion returned (#454)
IO::Astrodynamics::API::DTO::FrameTransformationDTO
TransformFrameProxy(const char *fromFrame, const char *toFrame, double epoch)
{
    ActivateErrorManagement();
    ClearLastError();
    IO::Astrodynamics::API::DTO::FrameTransformationDTO frameTransformationDto;
    auto failed = [&frameTransformationDto]
    {
        const double nan = std::nan("");
        frameTransformationDto.Rotation.w = frameTransformationDto.Rotation.x = nan;
        frameTransformationDto.Rotation.y = frameTransformationDto.Rotation.z = nan;
        frameTransformationDto.AngularVelocity.x = frameTransformationDto.AngularVelocity.y = frameTransformationDto.AngularVelocity.z = nan;
        return frameTransformationDto;
    };
    try
    {
        IO::Astrodynamics::Frames::Frames from{fromFrame};
        IO::Astrodynamics::Frames::Frames to{toFrame};
        IO::Astrodynamics::Time::TDB tdb((std::chrono::duration<double>(epoch)));
        auto mtx = from.ToFrame6x6(to, tdb);
        if (!ReportNativeErrors({}))
        {
            return failed();
        }

        IO::Astrodynamics::Math::Quaternion q(mtx);
        auto rawData = mtx.GetRawData();

        //Initialize data
        SpiceDouble rotation[3][3]{};
        SpiceDouble av[3]{};
        SpiceDouble convertedMtx[6][6];
        for (int i = 0; i < 6; ++i)
        {
            for (int j = 0; j < 6; ++j)
            {
                convertedMtx[i][j] = rawData[i][j];
            }
        }

        xf2rav_c(convertedMtx, rotation, av);
        if (!ReportNativeErrors({}))
        {
            return failed();
        }
        frameTransformationDto.Rotation = ToQuaternionDTO(q);
        frameTransformationDto.AngularVelocity = ToVector3DDTO(av);
        return frameTransformationDto;
    }
    catch (const std::exception &e)
    {
        ReportException(e);
        return failed();
    }
}

// No C++ exception crosses P/Invoke and no failure is silent (#711): an exception (the native Earth, the TLE), or a SPICE error
// (the Earth's GM missing before the kernels are loaded, SGP4's error), gives a NaN state with the exception's message then
// SPICE's in lastError, SPICE reset; before, a zero state with the message dropped, or an exception across P/Invoke
IO::Astrodynamics::API::DTO::StateVectorDTO ConvertTLEToStateVectorProxy(
        const char *L1, const char *L2, const char *L3, double epoch)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    IO::Astrodynamics::API::DTO::StateVectorDTO svDTO;
    try
    {
        auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
        std::string strings[3] = {L1, L2, L3};
        IO::Astrodynamics::OrbitalParameters::TLE tle(earth, strings);
        auto sv = tle.ToStateVector(IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(epoch)));
        svDTO = ToStateVectorDTO(sv);
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    if (!ReportNativeErrors(std::move(message)))
    {
        return FailedState(epoch, 399, IO::Astrodynamics::Frames::BodyFixedFrames::TEME().ToCharArray());
    }

    return svDTO;
}

// No C++ exception crosses P/Invoke and no failure is silent (#711): the centre is built as a celestial body
// (CelestialBody(int)), which throws SDKException when SPICE does not name it (a spacecraft whose GM a kernel gives: the
// process ended) and fails through SPICE when the kernel pool has no GM for it (a zero state was returned). Either gives a
// NaN state (NaN right ascension, declination and range for the equatorial coordinates), the epoch, the centre and the
// frame as given, with the exception's message then SPICE's in lastError, SPICE reset
IO::Astrodynamics::API::DTO::StateVectorDTO
ConvertConicElementsToStateVectorProxy(IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElementsDto)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    IO::Astrodynamics::API::DTO::StateVectorDTO svDTO;
    try
    {
        auto centerOfMotion = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(
                conicOrbitalElementsDto.centerOfMotionId);
        IO::Astrodynamics::Time::TDB tdb{std::chrono::duration<double>(conicOrbitalElementsDto.epoch)};
        IO::Astrodynamics::Frames::Frames frame{conicOrbitalElementsDto.frame};
        IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conicOrbitalElements{
                centerOfMotion,
                conicOrbitalElementsDto.perifocalDistance,
                conicOrbitalElementsDto.eccentricity,
                conicOrbitalElementsDto.inclination,
                conicOrbitalElementsDto.ascendingNodeLongitude,
                conicOrbitalElementsDto.periapsisArgument,
                conicOrbitalElementsDto.meanAnomaly,
                tdb,
                frame
        };
        auto sv = conicOrbitalElements.ToStateVector();
        svDTO = ToStateVectorDTO(sv);
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    if (!ReportNativeErrors(std::move(message)))
    {
        return FailedState(conicOrbitalElementsDto.epoch, conicOrbitalElementsDto.centerOfMotionId, conicOrbitalElementsDto.frame);
    }
    return svDTO;
}

IO::Astrodynamics::API::DTO::StateVectorDTO
ConvertEquinoctialElementsToStateVectorProxy(
        IO::Astrodynamics::API::DTO::EquinoctialElementsDTO equinoctialElementsDto)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    IO::Astrodynamics::API::DTO::StateVectorDTO svDTO;
    try
    {
        auto centerOfMotion = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(
                equinoctialElementsDto.centerOfMotionId);
        IO::Astrodynamics::Time::TDB tdb{std::chrono::duration<double>(equinoctialElementsDto.epoch)};
        IO::Astrodynamics::Frames::Frames frame{equinoctialElementsDto.inertialFrame};

        IO::Astrodynamics::OrbitalParameters::EquinoctialElements eq{
                centerOfMotion, tdb,
                equinoctialElementsDto.semiMajorAxis,
                equinoctialElementsDto.h, equinoctialElementsDto.k,
                equinoctialElementsDto.p, equinoctialElementsDto.q,
                equinoctialElementsDto.L,
                equinoctialElementsDto.periapsisLongitudeRate,
                equinoctialElementsDto.ascendingNodeLongitudeRate,
                equinoctialElementsDto.rightAscensionOfThePole,
                equinoctialElementsDto.declinationOfThePole, frame
        };

        auto sv = eq.ToStateVector();
        svDTO = ToStateVectorDTO(sv);
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    if (!ReportNativeErrors(std::move(message)))
    {
        return FailedState(equinoctialElementsDto.epoch, equinoctialElementsDto.centerOfMotionId, equinoctialElementsDto.inertialFrame);
    }
    return svDTO;
}

IO::Astrodynamics::API::DTO::RaDecDTO
ConvertStateVectorToEquatorialCoordinatesProxy(IO::Astrodynamics::API::DTO::StateVectorDTO stateVectorDto)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    IO::Astrodynamics::API::DTO::RaDecDTO raDecDTO;
    try
    {
        auto centerOfMotion = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(stateVectorDto.centerOfMotionId);
        IO::Astrodynamics::Time::TDB tdb{std::chrono::duration<double>(stateVectorDto.epoch)};
        IO::Astrodynamics::Frames::Frames frame{stateVectorDto.inertialFrame};
        IO::Astrodynamics::OrbitalParameters::StateVector sv{
                centerOfMotion, ToVector3D(stateVectorDto.position),
                ToVector3D(stateVectorDto.velocity), tdb, frame
        };
        auto raDec = sv.ToEquatorialCoordinates();
        raDecDTO = ToEquatorialDTO(raDec);
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    if (!ReportNativeErrors(std::move(message)))
    {
        raDecDTO.rightAscension = raDecDTO.declination = raDecDTO.range = std::nan("");
    }
    return raDecDTO;
}

// The state of one epoch (spkezr_c), the body of ReadEphemerisAtGivenEpochProxy and of each epoch of ReadEphemerisProxy: a failed
// read is a NaN state, its SPICE message kept in lastError, SPICE reset (#454). It neither clears lastError nor
// replaces a message already there, so that ReadEphemerisProxy, which clears it once before its loop, keeps the message
// of its first failed epoch, where the .NET layer stops once the loop has ended (#714, decision 1 of #719, decided by
// S. Guillet, 2026-11-17; before, each failed epoch replaced the message, so the last one was named)
static IO::Astrodynamics::API::DTO::StateVectorDTO ReadStateAt(double epoch, int observerId, int targetId, const char *frame,
                                                              const char *aberration)
{
    IO::Astrodynamics::API::DTO::StateVectorDTO stateVectorDto;
    SpiceDouble sv[6];
    SpiceDouble lt;
    spkezr_c(std::to_string(targetId).c_str(), epoch, frame, aberration, std::to_string(observerId).c_str(), sv, &lt);

    stateVectorDto.centerOfMotionId = observerId;

    stateVectorDto.epoch = epoch;
    stateVectorDto.SetFrame(frame);
    stateVectorDto.position.x = sv[0] * 1000.0;
    stateVectorDto.position.y = sv[1] * 1000.0;
    stateVectorDto.position.z = sv[2] * 1000.0;
    stateVectorDto.velocity.x = sv[3] * 1000.0;
    stateVectorDto.velocity.y = sv[4] * 1000.0;
    stateVectorDto.velocity.z = sv[5] * 1000.0;

    if (failed_c())
    {
        // #454: a failed read is a NaN state, its SPICE message kept for GetLastErrorProxy, the first of a call (#714)
        const char *message = HandleError();
        if (lastError[0] == '\0')
        {
            std::strncpy(lastError, message, sizeof(lastError) - 1);
            lastError[sizeof(lastError) - 1] = '\0';
        }
        stateVectorDto.position = stateVectorDto.velocity = {std::nan(""), std::nan(""), std::nan("")};
    }
    return stateVectorDto;
}

IO::Astrodynamics::API::DTO::StateVectorDTO ReadEphemerisAtGivenEpochProxy(
        double epoch, int observerId, int targetId, const char *frame, const char *aberration)
{
    ActivateErrorManagement();
    ClearLastError();
    return ReadStateAt(epoch, observerId, targetId, frame, aberration);
}

// As ConvertTLEToStateVectorProxy (#711): a failure gives NaN in every field, the epoch included, with the message in
// lastError; before, the epoch getelm_c never wrote (skipped on a pending error), which the .NET TLE cached for its life
IO::Astrodynamics::API::DTO::TLEElementsDTO GetTLEElementsProxy(const char *L1, const char *L2, const char *L3)
{
    ActivateErrorManagement();
    ClearLastError();
    std::string message;
    IO::Astrodynamics::API::DTO::TLEElementsDTO tleElementsDto;
    try
    {
        std::string lines[3]{L1, L2, L3};
        auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
        IO::Astrodynamics::OrbitalParameters::TLE tle(earth, lines);

        tleElementsDto.A = tle.GetSemiMajorAxis();
        tleElementsDto.E = tle.GetEccentricity();
        tleElementsDto.I = tle.GetInclination();
        tleElementsDto.O = tle.GetRightAscendingNodeLongitude();
        tleElementsDto.W = tle.GetPeriapsisArgument();
        tleElementsDto.M = tle.GetMeanAnomaly();

        tleElementsDto.Epoch = tle.GetEpoch().GetSecondsFromJ2000().count();

        tleElementsDto.BalisticCoefficient = tle.GetBalisticCoefficient();
        tleElementsDto.DragTerm = tle.GetDragTerm();
        tleElementsDto.SecondDerivativeOfMeanMotion = tle.GetSecondDerivativeOfMeanMotion();
    }
    catch (const std::exception &e)
    {
        message = e.what();
    }
    if (!ReportNativeErrors(std::move(message)))
    {
        const double nan = std::nan("");
        tleElementsDto.A = tleElementsDto.E = tleElementsDto.I = tleElementsDto.O = tleElementsDto.W = tleElementsDto.M = nan;
        tleElementsDto.Epoch = tleElementsDto.BalisticCoefficient = tleElementsDto.DragTerm = nan;
        tleElementsDto.SecondDerivativeOfMeanMotion = nan;
    }
    return tleElementsDto;
}

void KClearProxy()
{
    kclear_c();
}

#pragma endregion

// As Propagate2BodiesProxy (#714): oscltx_c's errors (SPICE(NONPOSITIVEMASS) for a GM not above 0, SPICE(DEGENERATECASE) for a
// zero position, a zero velocity or r and v too close to parallel, oscelt.c) give NaN in every element, the epoch, the centre
// and the frame as given, with SPICE's message in lastError, SPICE reset; before, the process aborted on a first call, and the
// elements oscltx_c never wrote were returned otherwise
IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO ConvertStateVectorToConicOrbitalElementProxy(
        IO::Astrodynamics::API::DTO::StateVectorDTO stateVector, double mu)
{
    ActivateErrorManagement();
    ClearLastError();
    ConstSpiceDouble sv[6]{
            stateVector.position.x, stateVector.position.y, stateVector.position.z,
            stateVector.velocity.x, stateVector.velocity.y, stateVector.velocity.z
    };
    SpiceDouble elts[SPICE_OSCLTX_NELTS]{};
    oscltx_c(sv, stateVector.epoch, mu, elts);
    IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElementsDto;
    conicOrbitalElementsDto.centerOfMotionId = stateVector.centerOfMotionId;
    conicOrbitalElementsDto.SetFrame(stateVector.inertialFrame);
    if (!ReportNativeErrors({}))
    {
        const double nan = std::nan("");
        conicOrbitalElementsDto.perifocalDistance = conicOrbitalElementsDto.eccentricity = conicOrbitalElementsDto.inclination = nan;
        conicOrbitalElementsDto.ascendingNodeLongitude = conicOrbitalElementsDto.periapsisArgument = nan;
        conicOrbitalElementsDto.meanAnomaly = conicOrbitalElementsDto.trueAnomaly = nan;
        conicOrbitalElementsDto.semiMajorAxis = conicOrbitalElementsDto.orbitalPeriod = nan;
        conicOrbitalElementsDto.epoch = stateVector.epoch;
        return conicOrbitalElementsDto;
    }
    conicOrbitalElementsDto.perifocalDistance = elts[0];
    conicOrbitalElementsDto.eccentricity = elts[1];
    conicOrbitalElementsDto.inclination = elts[2];
    conicOrbitalElementsDto.ascendingNodeLongitude = elts[3];
    conicOrbitalElementsDto.periapsisArgument = elts[4];
    conicOrbitalElementsDto.meanAnomaly = elts[5];
    conicOrbitalElementsDto.epoch = elts[6];
    conicOrbitalElementsDto.trueAnomaly = elts[8];
    conicOrbitalElementsDto.semiMajorAxis = elts[9];
    conicOrbitalElementsDto.orbitalPeriod = elts[10];
    return conicOrbitalElementsDto;
}

// No failure is silent and none aborts the process (#714): prop2b_c's errors (SPICE(NONPOSITIVEMASS) for a GM not above 0,
// SPICE(ZEROPOSITION), SPICE(ZEROVELOCITY), SPICE(NONCONICMOTION) for r parallel to v, SPICE(DTOUTOFRANGE)) give the NaN state
// at the epoch plus dt, the centre and the frame as given, with SPICE's message in lastError, SPICE reset. Before, the error
// management was not activated (SPICE's default action, ABORT, ended a process that had made no other call) and the state
// prop2b_c never wrote, the stack's contents, was returned with no message. A dt that is NaN raises no SPICE error: prop2b_c
// returns the initial state (measured with CSPICE N0067); the .NET layer cannot pass one (TimeSpan)
IO::Astrodynamics::API::DTO::StateVectorDTO Propagate2BodiesProxy(
        IO::Astrodynamics::API::DTO::StateVectorDTO stateVector, double mu, double dt)
{
    ActivateErrorManagement();
    ClearLastError();
    ConstSpiceDouble sv[6] = {
            stateVector.position.x, stateVector.position.y, stateVector.position.z,
            stateVector.velocity.x, stateVector.velocity.y, stateVector.velocity.z
    };

    SpiceDouble result[6]{};

    prop2b_c(mu, sv, dt, result);
    if (!ReportNativeErrors({}))
    {
        return FailedState(stateVector.epoch + dt, stateVector.centerOfMotionId, stateVector.inertialFrame);
    }

    IO::Astrodynamics::API::DTO::StateVectorDTO stateVectorDto;
    stateVectorDto.position.x = result[0];
    stateVectorDto.position.y = result[1];
    stateVectorDto.position.z = result[2];
    stateVectorDto.velocity.x = result[3];
    stateVectorDto.velocity.y = result[4];
    stateVectorDto.velocity.z = result[5];
    stateVectorDto.epoch = stateVector.epoch + dt;
    stateVectorDto.centerOfMotionId = stateVector.centerOfMotionId;
    stateVectorDto.SetFrame(stateVector.inertialFrame);
    return stateVectorDto;
}

// No failure is silent and none is left pending (#711): a failed conics_c (SPICE(BADGM) for a GM not above 0,
// SPICE(BADPERIAPSEVALUE), SPICE(BADECCENTRICITY)) gives the NaN state, the epoch, the centre and the frame as given, with
// SPICE's message in lastError, SPICE reset. Before, the error management was not activated (SPICE's default action aborted a
// process that had made no other call) and SPICE was not checked: the state conics_c never wrote was returned and SPICE left
// failed, so that the next call, GetCelestialBodyInfoProxy(399) for instance, returned the record of Id -1
IO::Astrodynamics::API::DTO::StateVectorDTO
ConvertConicElementsToStateVectorAtEpochProxy(IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElements, double epoch, double gm)
{
    ActivateErrorManagement();
    ClearLastError();
    SpiceDouble sv[6];

    double ke[8] = {conicOrbitalElements.perifocalDistance, conicOrbitalElements.eccentricity, conicOrbitalElements.inclination, conicOrbitalElements.ascendingNodeLongitude, conicOrbitalElements.periapsisArgument,
                    conicOrbitalElements.meanAnomaly, conicOrbitalElements.epoch, gm};
    conics_c(ke, epoch, sv);
    if (!ReportNativeErrors({}))
    {
        return FailedState(epoch, conicOrbitalElements.centerOfMotionId, conicOrbitalElements.frame);
    }

    IO::Astrodynamics::API::DTO::StateVectorDTO svres;
    svres.position.x = sv[0];
    svres.position.y = sv[1];
    svres.position.z = sv[2];

    svres.velocity.x = sv[3];
    svres.velocity.y = sv[4];
    svres.velocity.z = sv[5];

    svres.centerOfMotionId=conicOrbitalElements.centerOfMotionId;
    svres.SetFrame(conicOrbitalElements.frame);
    svres.epoch=epoch;

    return svres;

}
