#include <gtest/gtest.h>
#include <TDB.h>
#include <UTC.h>
#include <string>
#include <chrono>
#include <TimeSpan.h>

using namespace std::chrono_literals;

TEST(TDB, ToString)
{
	IO::SDK::Time::TDB dt("2021-01-15 12:05:16.627484 TDB");
	auto str = dt.ToString();
	ASSERT_STREQ("2021-01-15 12:05:16.627483 (TDB)", str.c_str());
}

TEST(TDB, GetTDB)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627488");
	ASSERT_DOUBLE_EQ(663984385.81183434, dt.GetSecondsFromJ2000().count());
}

TEST(TDB, SubstractTDB)
{
	IO::SDK::Time::TDB dt("2021-01-15 12:05:16.627488 TDB");
	IO::SDK::Time::TDB d2("2021-01-17 12:05:16.627488 TDB");
	auto delta = d2 - dt;
	ASSERT_DOUBLE_EQ(48.0, delta.GetHours().count());
}

TEST(TDB, Add)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627488");
	auto newDate = dt + IO::SDK::Time::TimeSpan(48.5h);
	ASSERT_STREQ("2021-01-17 12:36:25.811834 (TDB)", newDate.ToString().c_str());
}

TEST(TDB, SubstractTimeSpan)
{
	IO::SDK::Time::TDB dt("2021-01-01T06:00:00.0");
	auto newDate = dt - IO::SDK::Time::TimeSpan(2.5h);
	ASSERT_DOUBLE_EQ(662743869.18393588, newDate.GetSecondsFromJ2000().count());
}

TEST(TDB, Equal)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627488");
	IO::SDK::Time::TDB dt2("2021-01-15T12:05:16.627488");
	ASSERT_TRUE(dt == dt2);
}

TEST(TDB, NotEqual)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627488");
	IO::SDK::Time::TDB dt2("2021-01-15T12:05:16.627489");
	ASSERT_TRUE(dt != dt2);
}

TEST(TDB, GreaterThan)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627490");
	IO::SDK::Time::TDB dt2("2021-01-15T12:05:16.627489");
	ASSERT_TRUE(dt > dt2);
}

TEST(TDB, LowerThan)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627490");
	IO::SDK::Time::TDB dt2("2021-01-15T12:05:16.627489");
	ASSERT_TRUE(dt2 < dt);
}

TEST(TDB, GreaterThanOrEqual)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627490");
	IO::SDK::Time::TDB dt2("2021-01-15T12:05:16.627489");
	ASSERT_TRUE(dt >= dt2);

	IO::SDK::Time::TDB dt3("2021-01-15T12:05:16.627489");
	IO::SDK::Time::TDB dt4("2021-01-15T12:05:16.627489");
	ASSERT_TRUE(dt >= dt2);
}

TEST(TDB, LowerThanOrEqual)
{
	IO::SDK::Time::TDB dt("2021-01-15T12:05:16.627490");
	IO::SDK::Time::TDB dt2("2021-01-15T12:05:16.627489");
	ASSERT_TRUE(dt2 <= dt);

	IO::SDK::Time::TDB dt3("2021-01-15T12:05:16.627489");
	IO::SDK::Time::TDB dt4("2021-01-15T12:05:16.627489");
	ASSERT_TRUE(dt2 <= dt);
}

TEST(TDB, ToJulian)
{
	IO::SDK::Time::TDB tdb{"2021-01-15 12:05:16.627484 TDB"};
	ASSERT_DOUBLE_EQ(2459230.0036646700, tdb.ToJulian());
}

TEST(TDB, ToUTC)
{
	IO::SDK::Time::TDB tdb{"2010-06-21 00:07:06.184395 TDB"};
	ASSERT_STREQ("2010-06-21 00:05:59.999999 (UTC)", tdb.ToUTC().ToString().c_str());
}
