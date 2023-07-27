/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>

#include <UTC.h>
#include <TDB.h>
#include <JulianDate.h>

TEST(Julian, ToString)
{
    IO::Astrodynamics::Time::JulianDate julian{std::chrono::duration<double, std::ratio<86400>>(2451545.0)};
    ASSERT_STREQ("2451545.000000 JDTDB", julian.ToString().c_str());
}

TEST(Julian, ToTDB)
{
    IO::Astrodynamics::Time::JulianDate julian{std::chrono::duration<double, std::ratio<86400>>(2451545.0)};
    ASSERT_DOUBLE_EQ(0.0, julian.ToTDB().GetSecondsFromJ2000().count());
}

TEST(Julian, ToUTC)
{
    IO::Astrodynamics::Time::JulianDate julian{std::chrono::duration<double, std::ratio<86400>>(2451545.0)};
    ASSERT_DOUBLE_EQ(-64.183927263223808, julian.ToUTC().GetSecondsFromJ2000().count());
}

TEST(Julian, Add)
{
    IO::Astrodynamics::Time::JulianDate julian{std::chrono::duration<double, std::ratio<86400>>(2451545.0)};
    auto res = julian + IO::Astrodynamics::Time::TimeSpan(std::chrono::hours(12));
    ASSERT_DOUBLE_EQ(2451545.5, res.GetJulianDate().count());
}

TEST(Julian, Subtract)
{
    IO::Astrodynamics::Time::JulianDate julian{std::chrono::duration<double, std::ratio<86400>>(2451545.0)};
    IO::Astrodynamics::Time::JulianDate julian2{std::chrono::duration<double, std::ratio<86400>>(2451545.5)};
    auto res = julian2 - julian;
    ASSERT_DOUBLE_EQ(12.0, res.GetHours().count());
}

