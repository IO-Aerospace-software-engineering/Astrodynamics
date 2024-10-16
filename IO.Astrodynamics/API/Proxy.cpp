/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <algorithm>
#include <iostream>
#include <filesystem>


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

void LaunchProxy(IO::Astrodynamics::API::DTO::LaunchDTO &launchDto)
{
    ActivateErrorManagement();
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
        throw IO::Astrodynamics::Exception::SDKException(HandleError());
    }
}

bool WriteEphemerisProxy(const char *filePath, int objectId, IO::Astrodynamics::API::DTO::StateVectorDTO *sv,
                         unsigned int size)
{
    ActivateErrorManagement();
    IO::Astrodynamics::Kernels::EphemerisKernel kernel(filePath, objectId);

    std::vector<IO::Astrodynamics::OrbitalParameters::StateVector> states;
    states.reserve(size);
    std::map<int, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody>> celestialBodies;


    for (unsigned int i = 0; i < size; ++i)
    {
        if (celestialBodies.find(sv[0].centerOfMotionId) == celestialBodies.end())
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
    if (failed_c())
    {
        HandleError();
        return false;
    }
    return true;
}

bool WriteOrientationProxy(const char *filePath, int objectId, IO::Astrodynamics::API::DTO::StateOrientationDTO *so,
                           unsigned int size)
{
    ActivateErrorManagement();
    std::filesystem::path path(filePath);
    std::filesystem::path parentPath = path.parent_path();
    IO::Astrodynamics::Kernels::OrientationKernel kernel(filePath, objectId, objectId * 1000);

    std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> states;
    states.emplace_back();

    states.back().reserve(size);
    std::map<int, std::shared_ptr<IO::Astrodynamics::Body::CelestialBody>> celestialBodies;

    for (unsigned int i = 0; i < size; ++i)
    {
        states.back().emplace_back(ToQuaternion(so[i].orientation), ToVector3D(so[i].angularVelocity),
                                   IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(so[i].epoch)),
                                   IO::Astrodynamics::Frames::Frames(so[i].frame));
    }
    kernel.WriteOrientations(states);
    if (failed_c())
    {
        HandleError();
        return false;
    }
    return true;
}

void ReadOrientationProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int spacecraftId, double tolerance,
                          const char *frame,
                          double stepSize, IO::Astrodynamics::API::DTO::StateOrientationDTO *so)
{
    ActivateErrorManagement();
    if ((searchWindow.end - searchWindow.start) / stepSize > 10000)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException(
                "Step size to small or search window to large. The number of State orientation must be lower than 10000");
    }
    //Build platform id
    SpiceInt id = spacecraftId * 1000;

    double epoch = searchWindow.start;
    int idx{0};
    while (epoch <= searchWindow.end)
    {
        //Get encoded clock
        SpiceDouble sclk = IO::Astrodynamics::Kernels::SpacecraftClockKernel::ConvertToEncodedClock(spacecraftId,
                                                                                                    IO::Astrodynamics::Time::TDB(
                                                                                                            std::chrono::duration<double>(
                                                                                                                    epoch)));

        SpiceDouble cmat[3][3];
        SpiceDouble av[3];
        SpiceDouble clkout;
        SpiceBoolean found;

        //Get orientation and angular velocity
        ckgpav_c(id, sclk, tolerance, frame, cmat, av, &clkout, &found);

        if (!found)
        {
            throw IO::Astrodynamics::Exception::SDKException("No orientation found");
        }

        //Build array pointers
        double **arrayCmat;
        arrayCmat = new double *[3];
        for (int i = 0; i < 3; i++)
        {
            arrayCmat[i] = new double[3]{};
        }

        for (size_t i = 0; i < 3; i++)
        {
            for (size_t j = 0; j < 3; j++)
            {
                arrayCmat[i][j] = cmat[i][j];
            }
        }

        IO::Astrodynamics::Math::Quaternion q(IO::Astrodynamics::Math::Matrix(3, 3, arrayCmat));

        //Free memory
        for (int i = 0; i < 3; i++)
            delete[] arrayCmat[i];
        delete[] arrayCmat;

        double correctedEpoch{};
        sct2e_c(spacecraftId, sclk, &correctedEpoch);
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
        HandleError();
    }
}

bool LoadKernelsProxy(const char *path)
{
    ActivateErrorManagement();
    IO::Astrodynamics::Kernels::KernelsLoader::Load(path);
    if (failed_c())
    {
        return false;
        HandleError();
    }
    return true;
}

bool UnloadKernelsProxy(const char *path)
{
    ActivateErrorManagement();
    IO::Astrodynamics::Kernels::KernelsLoader::Unload(path);
    if (failed_c())
    {
        HandleError();
        return false;
    }
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

void ReadEphemerisProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId, int targetId,
                        const char *frame, const char *aberration, double stepSize,
                        IO::Astrodynamics::API::DTO::StateVectorDTO *stateVectors)
{
    ActivateErrorManagement();
    if ((searchWindow.end - searchWindow.start) / stepSize > 10000)
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException(
                "Step size to small or search window to large. The number of State vector must be lower than 10000");
    }
    int idx = 0;
    double epoch = searchWindow.start;
    while (epoch <= searchWindow.end)
    {
        stateVectors[idx] = ReadEphemerisAtGivenEpochProxy(epoch, observerId, targetId, frame, aberration);
        epoch += stepSize;
        idx++;
    }
    if (failed_c())
    {
        HandleError();
    }
}

void FindWindowsOnDistanceConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                          int targetId,
                                          const char *relationalOperator, double value, const char *aberration,
                                          double stepSize, IO::Astrodynamics::API::DTO::WindowDTO windows[1000])
{
    ActivateErrorManagement();
    auto relationalOpe = IO::Astrodynamics::Constraints::RelationalOperator::ToRelationalOperator(relationalOperator);
    auto abe = IO::Astrodynamics::Aberrations::ToEnum(aberration);

    auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsOnDistanceConstraint(
            ToTDBWindow(searchWindow),
            observerId, targetId,
            relationalOpe, value, abe,
            IO::Astrodynamics::Time::TimeSpan(stepSize));
    for (size_t i = 0; i < res.size(); ++i)
    {
        windows[i] = ToWindowDTO(res[i]);
    }
    if (failed_c())
    {
        HandleError();
    }
}

void
FindWindowsOnOccultationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                        int targetId,
                                        const char *targetFrame, const char *targetShape, int frontBodyId,
                                        const char *frontFrame, const char *frontShape, const char *occultationType,
                                        const char *aberration, double stepSize,
                                        IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    ActivateErrorManagement();
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

    for (size_t i = 0; i < res.size(); ++i)
    {
        windows[i] = ToWindowDTO(res[i]);
    }
    if (failed_c())
    {
        HandleError();
    }
}

void
FindWindowsOnCoordinateConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                       int targetId,
                                       const char *frame, const char *coordinateSystem,
                                       const char *coordinate,
                                       const char *relationalOperator, double value, double adjustValue,
                                       const char *aberration, double stepSize,
                                       IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    ActivateErrorManagement();
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

    for (size_t i = 0; i < res.size(); ++i)
    {
        windows[i] = ToWindowDTO(res[i]);
    }
    if (failed_c())
    {
        HandleError();
    }
}

void FindWindowsOnIlluminationConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                              const char *illuminationSource, int targetBody, const char *fixedFrame,
                                              IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic,
                                              const char *illuminationType,
                                              const char *relationalOperator, double value,
                                              double adjustValue,
                                              const char *aberration, double stepSize, const char *method,
                                              IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    ActivateErrorManagement();
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
    for (size_t i = 0; i < res.size(); ++i)
    {
        windows[i] = ToWindowDTO(res[i]);
    }
    if (failed_c())
    {
        HandleError();
    }
}

void
FindWindowsInFieldOfViewConstraintProxy(IO::Astrodynamics::API::DTO::WindowDTO searchWindow, int observerId,
                                        int instrumentId,
                                        int targetId, const char *targetFrame,
                                        const char *targetShape,
                                        const char *aberration, double stepSize,
                                        IO::Astrodynamics::API::DTO::WindowDTO *windows)
{
    ActivateErrorManagement();
    auto abe = IO::Astrodynamics::Aberrations::ToEnum(aberration);
    auto res = IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(
            ToTDBWindow(searchWindow),
            observerId, instrumentId,
            targetId, targetFrame,
            targetShape,
            abe, IO::Astrodynamics::Time::TimeSpan(
                    stepSize));
    for (size_t i = 0; i < res.size(); ++i)
    {
        windows[i] = ToWindowDTO(res[i]);
    }
    if (failed_c())
    {
        HandleError();
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

IO::Astrodynamics::API::DTO::CelestialBodyDTO GetCelestialBodyInfoProxy(int bodyId)
{
    ActivateErrorManagement();
    IO::Astrodynamics::API::DTO::CelestialBodyDTO res;

    SpiceChar name[32]{};
    SpiceBoolean found{false};
    bodc2n_c(bodyId, 32, name, &found);
    if (found)
    {
        res.Id = bodyId;
        res.SetName(name);
        res.CenterOfMotionId = IO::Astrodynamics::Body::CelestialBody::FindCenterOfMotionId(bodyId);
        res.BarycenterOfMotionId = IO::Astrodynamics::Body::CelestialBody::FindBarycenterOfMotionId(bodyId);

        SpiceInt dim;
        // Search body's radii
        if (bodyId >= 10)
        {
            SpiceDouble radiiRes[3];
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

        // Search CelestialItem's mass
        res.GM = IO::Astrodynamics::Body::CelestialBody::ReadGM(bodyId);

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

IO::Astrodynamics::API::DTO::FrameTransformationDTO
TransformFrameProxy(const char *fromFrame, const char *toFrame, double epoch)
{
    ActivateErrorManagement();
    IO::Astrodynamics::Frames::Frames from{fromFrame};
    IO::Astrodynamics::Frames::Frames to{toFrame};
    IO::Astrodynamics::Time::TDB tdb((std::chrono::duration<double>(epoch)));
    auto mtx = from.ToFrame6x6(to, tdb);
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

    IO::Astrodynamics::API::DTO::FrameTransformationDTO frameTransformationDto;
    xf2rav_c(convertedMtx, rotation, av);
    if (failed_c())
    {
        HandleError();
        return frameTransformationDto;
    }
    frameTransformationDto.Rotation = ToQuaternionDTO(q);
    frameTransformationDto.AngularVelocity = ToVector3DDTO(av);
    return frameTransformationDto;
}

IO::Astrodynamics::API::DTO::StateVectorDTO ConvertTLEToStateVectorProxy(
        const char *L1, const char *L2, const char *L3, double epoch)
{
    ActivateErrorManagement();
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::string strings[3] = {L1, L2, L3};
    IO::Astrodynamics::OrbitalParameters::TLE tle(earth, strings);
    auto sv = tle.ToStateVector(IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(epoch)));
    auto svDTO = ToStateVectorDTO(sv);
    if (failed_c())
    {
        HandleError();
    }

    return svDTO;
}

IO::Astrodynamics::API::DTO::StateVectorDTO
ConvertConicElementsToStateVectorProxy(IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElementsDto)
{
    ActivateErrorManagement();
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
    auto svDTO = ToStateVectorDTO(sv);
    if (failed_c())
    {
        HandleError();
    }
    return svDTO;
}

IO::Astrodynamics::API::DTO::StateVectorDTO
ConvertEquinoctialElementsToStateVectorProxy(
        IO::Astrodynamics::API::DTO::EquinoctialElementsDTO equinoctialElementsDto)
{
    ActivateErrorManagement();
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
    auto svDTO = ToStateVectorDTO(sv);
    if (failed_c())
    {
        HandleError();
    }
    return svDTO;
}

IO::Astrodynamics::API::DTO::RaDecDTO
ConvertStateVectorToEquatorialCoordinatesProxy(IO::Astrodynamics::API::DTO::StateVectorDTO stateVectorDto)
{
    ActivateErrorManagement();
    auto centerOfMotion = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(stateVectorDto.centerOfMotionId);
    IO::Astrodynamics::Time::TDB tdb{std::chrono::duration<double>(stateVectorDto.epoch)};
    IO::Astrodynamics::Frames::Frames frame{stateVectorDto.inertialFrame};
    IO::Astrodynamics::OrbitalParameters::StateVector sv{
            centerOfMotion, ToVector3D(stateVectorDto.position),
            ToVector3D(stateVectorDto.velocity), tdb, frame
    };
    auto raDec = sv.ToEquatorialCoordinates();
    if (failed_c())
    {
        HandleError();
    }
    return ToEquatorialDTO(raDec);
}

IO::Astrodynamics::API::DTO::StateVectorDTO ReadEphemerisAtGivenEpochProxy(
        double epoch, int observerId, int targetId, const char *frame, const char *aberration)
{
    ActivateErrorManagement();
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
        HandleError();
    }
    return stateVectorDto;
}

IO::Astrodynamics::API::DTO::TLEElementsDTO GetTLEElementsProxy(const char *L1, const char *L2, const char *L3)
{
    ActivateErrorManagement();
    std::string lines[3]{L1, L2, L3};
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    IO::Astrodynamics::OrbitalParameters::TLE tle(earth, lines);

    IO::Astrodynamics::API::DTO::TLEElementsDTO tleElementsDto;

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
    if (failed_c())
    {
        HandleError();
    }
    return tleElementsDto;
}

void KClearProxy()
{
    kclear_c();
}

#pragma endregion

IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO ConvertStateVectorToConicOrbitalElementProxy(
        IO::Astrodynamics::API::DTO::StateVectorDTO stateVector, double mu)
{
    ConstSpiceDouble sv[6]{
            stateVector.position.x, stateVector.position.y, stateVector.position.z,
            stateVector.velocity.x, stateVector.velocity.y, stateVector.velocity.z
    };
    SpiceDouble elts[SPICE_OSCLTX_NELTS];
    oscltx_c(sv, stateVector.epoch, mu, elts);
    IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElementsDto;
    conicOrbitalElementsDto.centerOfMotionId = stateVector.centerOfMotionId;
    conicOrbitalElementsDto.SetFrame(stateVector.inertialFrame);
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
    if (failed_c())
    {
        HandleError();
    }
    return conicOrbitalElementsDto;
}

IO::Astrodynamics::API::DTO::StateVectorDTO Propagate2BodiesProxy(
        IO::Astrodynamics::API::DTO::StateVectorDTO stateVector, double mu, double dt)
{
    ConstSpiceDouble sv[6] = {
            stateVector.position.x, stateVector.position.y, stateVector.position.z,
            stateVector.velocity.x, stateVector.velocity.y, stateVector.velocity.z
    };

    SpiceDouble result[6];

    prop2b_c(mu, sv, dt, result);

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
    if (failed_c())
    {
        HandleError();
    }
    return stateVectorDto;
}

IO::Astrodynamics::API::DTO::StateVectorDTO
ConvertConicElementsToStateVectorAtEpochProxy(IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conicOrbitalElements, double epoch, double gm)
{
    SpiceDouble sv[6];

    double ke[8] = {conicOrbitalElements.perifocalDistance, conicOrbitalElements.eccentricity, conicOrbitalElements.inclination, conicOrbitalElements.ascendingNodeLongitude, conicOrbitalElements.periapsisArgument,
                    conicOrbitalElements.meanAnomaly, conicOrbitalElements.epoch, gm};
    conics_c(ke, epoch, sv);

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
