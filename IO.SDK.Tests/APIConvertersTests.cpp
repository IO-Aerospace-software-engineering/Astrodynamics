//
// Created by sylvain guillet on 3/29/23.
//
#include <gtest/gtest.h>
#include "Converters.cpp"

TEST(APIConverters, Window)
{
    IO::SDK::API::DTO::WindowDTO window;
    window.start = 10.0;
    window.end = 20.0;

    auto res = ToWindow(window);

    ASSERT_DOUBLE_EQ(res.GetStartDate().GetSecondsFromJ2000().count(), 10.0);
    ASSERT_DOUBLE_EQ(res.GetEndDate().GetSecondsFromJ2000().count(), 20.0);

    auto dto = ToWindowDTO(res);
    ASSERT_DOUBLE_EQ(dto.start, 10.0);
    ASSERT_DOUBLE_EQ(dto.end, 20.0);
}

TEST(APIConverters, Vector)
{
    IO::SDK::API::DTO::Vector3DDTO vector3Ddto;
    vector3Ddto.x = 1.0;
    vector3Ddto.y = 2.0;
    vector3Ddto.z = 3.0;

    auto vector = ToVector3D(vector3Ddto);
    ASSERT_DOUBLE_EQ(1.0, vector.GetX());
    ASSERT_DOUBLE_EQ(2.0, vector.GetY());
    ASSERT_DOUBLE_EQ(3.0, vector.GetZ());

    auto dto = ToVector3DDTO(vector);
    ASSERT_DOUBLE_EQ(1.0, dto.x);
    ASSERT_DOUBLE_EQ(2.0, dto.y);
    ASSERT_DOUBLE_EQ(3.0, dto.z);
}

TEST(APIConverters, Quaternion)
{
    IO::SDK::Math::Quaternion q(1.0, 2.0, 3.0, 4.0);
    auto dto = ToQuaternionDTO(q);
    ASSERT_DOUBLE_EQ(1.0, dto.w);
    ASSERT_DOUBLE_EQ(2.0, dto.x);
    ASSERT_DOUBLE_EQ(3.0, dto.y);
    ASSERT_DOUBLE_EQ(4.0, dto.z);

    auto quaternion = ToQuaternion(dto);
    ASSERT_DOUBLE_EQ(1.0, quaternion.GetQ0());
    ASSERT_DOUBLE_EQ(2.0, quaternion.GetQ1());
    ASSERT_DOUBLE_EQ(3.0, quaternion.GetQ2());
    ASSERT_DOUBLE_EQ(4.0, quaternion.GetQ3());
}

TEST(APIConverters, Geodetic)
{
    IO::SDK::API::DTO::GeodeticDTO geodeticDTO;
    geodeticDTO.latitude = 1.0;
    geodeticDTO.longitude = 2.0;
    geodeticDTO.altitude = 3.0;

    auto geodetic = ToGeodetic(geodeticDTO);
    ASSERT_DOUBLE_EQ(1.0, geodetic.GetLatitude());
    ASSERT_DOUBLE_EQ(2.0, geodetic.GetLongitude());
    ASSERT_DOUBLE_EQ(3.0, geodetic.GetAltitude());

    auto dto = ToGeodeticDTO(geodetic);
    ASSERT_DOUBLE_EQ(1.0, dto.latitude);
    ASSERT_DOUBLE_EQ(2.0, dto.longitude);
    ASSERT_DOUBLE_EQ(3.0, dto.altitude);
}