#include <gtest/gtest.h>

#include <UTC.h>
#include <TDB.h>

TEST(UTC, ToString)
{
    IO::SDK::Time::UTC utc{"2021-01-15T12:05:16.627484"};
    ASSERT_STREQ("2021-01-15 12:05:16.627483 (UTC)", utc.ToString().c_str());
}

TEST(UTC, ToJulian)
{
    IO::SDK::Time::UTC utc{"2021-01-15T12:05:16.627484"};
    ASSERT_DOUBLE_EQ(2459230.0036646700, utc.ToJulian());
}

TEST(UTC, CenturiesFromJ2000)
{
    IO::SDK::Time::UTC utc{"2010-06-21T00:06:00"};
    ASSERT_DOUBLE_EQ(0.104681838923118, utc.CenturiesFromJ2000());
}

TEST(UTC, ToTDB)
{
    IO::SDK::Time::UTC utc{"2010-06-21T00:06:00"};
    ASSERT_STREQ("2010-06-21 00:07:06.184395 (TDB)", utc.ToTDB().ToString().c_str());
}

