//
// Created by spacer on 3/29/23.
//
#include <Window.h>
#include <WindowDTO.h>
#include <Vector3DDTO.h>
#include <GeodeticDTO.h>
#include <Geodetic.h>
#include <Quaternion.h>
#include <QuaternionDTO.h>

static IO::SDK::Time::Window<IO::SDK::Time::UTC> ToWindow(IO::SDK::API::DTO::WindowDTO &window)
{
    return IO::SDK::Time::Window<IO::SDK::Time::UTC>(IO::SDK::Time::UTC(std::chrono::duration<double>(window.start)),
                                                     IO::SDK::Time::UTC(std::chrono::duration<double>(window.end)));
}

static IO::SDK::API::DTO::WindowDTO ToWindowDTO(IO::SDK::Time::Window<IO::SDK::Time::UTC> &window)
{
    IO::SDK::API::DTO::WindowDTO dto;
    dto.start = window.GetStartDate().GetSecondsFromJ2000().count();
    dto.end = window.GetEndDate().GetSecondsFromJ2000().count();
    return dto;
}

static IO::SDK::Math::Vector3D ToVector3D(IO::SDK::API::DTO::Vector3DDTO &vector)
{
    return IO::SDK::Math::Vector3D(vector.x, vector.y, vector.z);
}

static IO::SDK::API::DTO::Vector3DDTO ToVector3DDTO(IO::SDK::Math::Vector3D &vector)
{
    IO::SDK::API::DTO::Vector3DDTO dto;
    dto.x = vector.GetX();
    dto.y = vector.GetY();
    dto.z = vector.GetZ();
    return dto;
}

static IO::SDK::Math::Quaternion ToQuaternion(IO::SDK::API::DTO::QuaternionDTO &dto)
{
    return IO::SDK::Math::Quaternion(dto.w, dto.x, dto.y, dto.z);
}

static IO::SDK::API::DTO::QuaternionDTO ToQuaternionDTO(IO::SDK::Math::Quaternion &quaternion)
{
    IO::SDK::API::DTO::QuaternionDTO q;
    q.w = quaternion.GetQ0();
    q.x = quaternion.GetQ1();
    q.y = quaternion.GetQ2();
    q.z = quaternion.GetQ3();
    return q;
}

static IO::SDK::Coordinates::Geodetic ToGeodetic(IO::SDK::API::DTO::GeodeticDTO &dto)
{
    return IO::SDK::Coordinates::Geodetic(dto.longitude, dto.latitude, dto.altitude);
}

static IO::SDK::API::DTO::GeodeticDTO ToGeodeticDTO(IO::SDK::Coordinates::Geodetic &geodetic)
{
    IO::SDK::API::DTO::GeodeticDTO dto;
    dto.latitude = geodetic.GetLatitude();
    dto.longitude = geodetic.GetLongitude();
    dto.altitude = geodetic.GetAltitude();
    return dto;

}