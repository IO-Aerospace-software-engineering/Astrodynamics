/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by sylvain guillet on 3/29/23.
//
#include <gtest/gtest.h>
#include "Converters.cpp"

TEST(APIConverters, WindowUTC)
{
    IO::Astrodynamics::API::DTO::WindowDTO window{};
    window.start = 10.0;
    window.end = 20.0;

    auto res = ToUTCWindow(window);

    ASSERT_DOUBLE_EQ(res.GetStartDate().GetSecondsFromJ2000().count(), 10.0);
    ASSERT_DOUBLE_EQ(res.GetEndDate().GetSecondsFromJ2000().count(), 20.0);

    auto dto = ToWindowDTO(res);
    ASSERT_DOUBLE_EQ(dto.start, 10.0);
    ASSERT_DOUBLE_EQ(dto.end, 20.0);
}

TEST(APIConverters, WindowTDB)
{
    IO::Astrodynamics::API::DTO::WindowDTO window{};
    window.start = 10.0;
    window.end = 20.0;

    auto res = ToTDBWindow(window);

    ASSERT_DOUBLE_EQ(res.GetStartDate().GetSecondsFromJ2000().count(), 10.0);
    ASSERT_DOUBLE_EQ(res.GetEndDate().GetSecondsFromJ2000().count(), 20.0);

    auto dto = ToWindowDTO(res);
    ASSERT_DOUBLE_EQ(dto.start, 10.0);
    ASSERT_DOUBLE_EQ(dto.end, 20.0);
}

TEST(APIConverters, Vector)
{
    IO::Astrodynamics::API::DTO::Vector3DDTO vector3Ddto{};
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
    IO::Astrodynamics::Math::Quaternion q(1.0, 2.0, 3.0, 4.0);
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
    IO::Astrodynamics::API::DTO::PlanetodeticDTO geodeticDTO(2.0, 1.0, 3.0);

    auto geodetic = ToGeodetic(geodeticDTO);
    ASSERT_DOUBLE_EQ(1.0, geodetic.GetLatitude());
    ASSERT_DOUBLE_EQ(2.0, geodetic.GetLongitude());
    ASSERT_DOUBLE_EQ(3.0, geodetic.GetAltitude());

    auto dto = ToGeodeticDTO(geodetic);
    ASSERT_DOUBLE_EQ(1.0, dto.latitude);
    ASSERT_DOUBLE_EQ(2.0, dto.longitude);
    ASSERT_DOUBLE_EQ(3.0, dto.altitude);
}

TEST (APIConverters, StateVector)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);
    auto sv = moon->GetOrbitalParametersAtEpoch()->ToStateVector();

    auto svDto = ToStateVectorDTO(sv);
    ASSERT_DOUBLE_EQ(sv.GetPosition().GetX(), svDto.position.x);
    ASSERT_DOUBLE_EQ(sv.GetPosition().GetY(), svDto.position.y);
    ASSERT_DOUBLE_EQ(sv.GetPosition().GetZ(), svDto.position.z);
    ASSERT_DOUBLE_EQ(sv.GetVelocity().GetX(), svDto.velocity.x);
    ASSERT_DOUBLE_EQ(sv.GetVelocity().GetY(), svDto.velocity.y);
    ASSERT_DOUBLE_EQ(sv.GetVelocity().GetZ(), svDto.velocity.z);
    ASSERT_DOUBLE_EQ(sv.GetEpoch().GetSecondsFromJ2000().count(), svDto.epoch);
    ASSERT_STREQ(sv.GetFrame().ToCharArray(), svDto.inertialFrame);
}

TEST (APIConverters, ConicOrbitalElement)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    IO::Astrodynamics::Time::TDB tdb{std::chrono::duration<double>(1000.0)};
    IO::Astrodynamics::Frames::Frames frame{"J2000"};
    IO::Astrodynamics::OrbitalParameters::ConicOrbitalElements conics{earth, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, tdb, frame};

    auto conicsDto = ToConicOrbitalElementDTo(conics);
    ASSERT_DOUBLE_EQ(conics.GetPerifocalDistance(),conicsDto.perifocalDistance);
    ASSERT_DOUBLE_EQ(conics.GetEccentricity(),conicsDto.eccentricity);
    ASSERT_DOUBLE_EQ(conics.GetInclination(),conicsDto.inclination);
    ASSERT_DOUBLE_EQ(conics.GetRightAscendingNodeLongitude(),conicsDto.ascendingNodeLongitude);
    ASSERT_DOUBLE_EQ(conics.GetPeriapsisArgument(),conicsDto.periapsisArgument);
    ASSERT_DOUBLE_EQ(conics.GetMeanAnomaly(),conicsDto.meanAnomaly);
    ASSERT_DOUBLE_EQ(conics.GetEpoch().GetSecondsFromJ2000().count(),conicsDto.epoch);
    ASSERT_STREQ(conics.GetFrame().ToCharArray(), conicsDto.frame);
}

TEST (APIConverters,EquatorialCoordinates)
{
    IO::Astrodynamics::Coordinates::Equatorial equatorial{1.0,2.0,3.0};
    auto equatorialDTO= ToEquatorialDTO(equatorial);

    ASSERT_DOUBLE_EQ(equatorial.GetRA(),equatorialDTO.rightAscension);
    ASSERT_DOUBLE_EQ(equatorial.GetDec(),equatorialDTO.declination);
    ASSERT_DOUBLE_EQ(equatorial.GetRange(),equatorialDTO.range);
}