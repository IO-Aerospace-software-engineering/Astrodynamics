#include <gtest/gtest.h>

#include <UTC.h>
#include <TDB.h>
#include <JulianDate.h>

TEST(Julian, ToString)
{
    IO::Astrodynamics::Time::JulianDate julian{std::chrono::duration<double>(2451545.0)};
    //ASSERT_STREQ("2451545.0 JDTB", julian.ToString().c_str());
}

TEST(Julian, ToJulian)
{
    IO::Astrodynamics::Time::UTC utc{"2021-01-15T12:05:16.627484"};
    ASSERT_DOUBLE_EQ(2459230.0036646700, utc.ToJulian());
}

TEST(Julian, CenturiesFromJ2000)
{
    IO::Astrodynamics::Time::UTC utc{"2010-06-21T00:06:00"};
    ASSERT_DOUBLE_EQ(0.104681838923118, utc.CenturiesFromJ2000());
}

TEST(Julian, ToTDB)
{
    IO::Astrodynamics::Time::UTC utc{"2010-06-21T00:06:00"};
    ASSERT_STREQ("2010-06-21 00:07:06.184395 (TDB)", utc.ToTDB().ToString().c_str());
}

