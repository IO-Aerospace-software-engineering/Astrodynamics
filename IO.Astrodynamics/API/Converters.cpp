/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <WindowDTO.h>
#include <PlanetodeticDTO.h>
#include <Quaternion.h>
#include <QuaternionDTO.h>
#include <StateVectorDTO.h>
#include <ConicOrbitalElementsDTO.h>
#include <ConicOrbitalElements.h>
#include <RaDecDTO.h>

static IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> ToUTCWindow(IO::Astrodynamics::API::DTO::WindowDTO &window)
{
    return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>{IO::Astrodynamics::Time::UTC(std::chrono::duration<double>(window.start)),
                                                     IO::Astrodynamics::Time::UTC(std::chrono::duration<double>(window.end))};
}

static IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> ToTDBWindow(IO::Astrodynamics::API::DTO::WindowDTO &window)
{
    return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>{IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(window.start)),
                                                     IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(window.end))};
}

static IO::Astrodynamics::API::DTO::WindowDTO ToWindowDTO(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &window)
{
    IO::Astrodynamics::API::DTO::WindowDTO dto{};
    dto.start = window.GetStartDate().GetSecondsFromJ2000().count();
    dto.end = window.GetEndDate().GetSecondsFromJ2000().count();
    return dto;
}

static IO::Astrodynamics::API::DTO::WindowDTO ToWindowDTO(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &window)
{
    IO::Astrodynamics::API::DTO::WindowDTO dto{};
    dto.start = window.GetStartDate().GetSecondsFromJ2000().count();
    dto.end = window.GetEndDate().GetSecondsFromJ2000().count();
    return dto;
}

static IO::Astrodynamics::Math::Vector3D ToVector3D(const IO::Astrodynamics::API::DTO::Vector3DDTO &vector)
{
    return {vector.x, vector.y, vector.z};
}

static IO::Astrodynamics::API::DTO::Vector3DDTO ToVector3DDTO(const IO::Astrodynamics::Math::Vector3D &vector)
{
    IO::Astrodynamics::API::DTO::Vector3DDTO dto{};
    dto.x = vector.GetX();
    dto.y = vector.GetY();
    dto.z = vector.GetZ();
    return dto;
}

static IO::Astrodynamics::API::DTO::Vector3DDTO ToVector3DDTO(const double data[3])
{
    IO::Astrodynamics::API::DTO::Vector3DDTO dto{};
    dto.x = data[0];
    dto.y = data[1];
    dto.z = data[2];
    return dto;
}

static IO::Astrodynamics::Math::Quaternion ToQuaternion(IO::Astrodynamics::API::DTO::QuaternionDTO &dto)
{
    return IO::Astrodynamics::Math::Quaternion{dto.w, dto.x, dto.y, dto.z};
}

static IO::Astrodynamics::API::DTO::QuaternionDTO ToQuaternionDTO(IO::Astrodynamics::Math::Quaternion &quaternion)
{
    IO::Astrodynamics::API::DTO::QuaternionDTO q;
    q.w = quaternion.GetQ0();
    q.x = quaternion.GetQ1();
    q.y = quaternion.GetQ2();
    q.z = quaternion.GetQ3();
    return q;
}

static IO::Astrodynamics::Coordinates::Planetodetic ToPlanetodetic(IO::Astrodynamics::API::DTO::PlanetodeticDTO &dto)
{
    return IO::Astrodynamics::Coordinates::Planetodetic{dto.longitude, dto.latitude, dto.altitude};
}

static IO::Astrodynamics::API::DTO::PlanetodeticDTO ToGeodeticDTO(IO::Astrodynamics::Coordinates::Planetodetic &geodetic)
{
    IO::Astrodynamics::API::DTO::PlanetodeticDTO dto(geodetic.GetLongitude(), geodetic.GetLatitude(), geodetic.GetAltitude());
    return dto;
}

static IO::Astrodynamics::API::DTO::StateVectorDTO ToStateVectorDTO(IO::Astrodynamics::OrbitalParameters::StateVector &stateVector)
{
    IO::Astrodynamics::API::DTO::StateVectorDTO dto{};
    dto.epoch = stateVector.GetEpoch().GetSecondsFromJ2000().count();
    dto.SetFrame(stateVector.GetFrame().ToCharArray());
    dto.centerOfMotionId = stateVector.GetCenterOfMotion()->GetId();
    dto.position = ToVector3DDTO(stateVector.GetPosition());
    dto.velocity = ToVector3DDTO(stateVector.GetVelocity());

    return dto;
}

static IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO
ToConicOrbitalElementDTo(IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements &conicOrbitalElements)
{
    IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO dto{};
    dto.epoch = conicOrbitalElements.GetEpoch().GetSecondsFromJ2000().count();
    dto.centerOfMotionId = conicOrbitalElements.GetCenterOfMotion()->GetId();
    dto.ascendingNodeLongitude = conicOrbitalElements.GetRightAscendingNodeLongitude();
    dto.eccentricity = conicOrbitalElements.GetEccentricity();
    dto.inclination = conicOrbitalElements.GetInclination();
    dto.meanAnomaly = conicOrbitalElements.GetMeanAnomaly();
    dto.orbitalPeriod = conicOrbitalElements.GetPeriod().GetSeconds().count();
    dto.periapsisArgument = conicOrbitalElements.GetPeriapsisArgument();
    dto.perifocalDistance = conicOrbitalElements.GetPerifocalDistance();
    dto.semiMajorAxis = conicOrbitalElements.GetSemiMajorAxis();
    dto.trueAnomaly = conicOrbitalElements.GetTrueAnomaly();
    dto.frame = strdup(conicOrbitalElements.GetFrame().ToCharArray());
    return dto;
}

static IO::Astrodynamics::API::DTO::RaDecDTO ToEquatorialDTO(IO::Astrodynamics::Coordinates::Equatorial &raDec)
{
    IO::Astrodynamics::API::DTO::RaDecDTO raDecDto;
    raDecDto.declination = raDec.GetDec();
    raDecDto.rightAscension = raDec.GetRA();
    raDecDto.range = raDec.GetRange();
    return raDecDto;
}