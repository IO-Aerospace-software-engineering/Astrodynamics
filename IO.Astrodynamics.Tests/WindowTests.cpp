#include <gtest/gtest.h>
#include <Window.h>
#include <TDB.h>
#include <chrono>

using namespace std::chrono_literals;

TEST(Window, Length)
{
	IO::Astrodynamics::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb2("2021-01-03 12:00:00 TDB");

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w(tdb, tdb2);
	ASSERT_DOUBLE_EQ(48.0, w.GetLength().GetHours().count());
}

TEST(Window, EndDate)
{
	IO::Astrodynamics::Time::TDB tdb("2021-01-01 12:00:00 UTC");
	IO::Astrodynamics::Time::TimeSpan ts(72.0h);

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w(tdb, ts);
	ASSERT_STREQ("2021-01-04 12:01:09.183943 (TDB)", w.GetEndDate().ToString().c_str());
}

TEST(Window, Equals)
{
	IO::Astrodynamics::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb2("2021-01-03 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb3("2021-01-05 12:00:00 TDB");

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w(tdb, tdb2);
	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w2(tdb, tdb2);
	ASSERT_EQ(w, w2);

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w3(tdb, tdb3);

	ASSERT_NE(w, w3);
}

TEST(Window, Intersects)
{
	IO::Astrodynamics::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb2("2021-01-03 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb3("2021-01-05 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb4("2021-01-07 12:00:00 TDB");

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w(tdb, tdb2);
	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w2(tdb, tdb2);
	ASSERT_TRUE(w.Intersects(w2));
	ASSERT_TRUE(w2.Intersects(w));

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w3(tdb2, tdb3);
	ASSERT_FALSE(w2.Intersects(w3));
	ASSERT_FALSE(w3.Intersects(w2));

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w4(tdb, tdb3);
	ASSERT_TRUE(w4.Intersects(w3));
	ASSERT_TRUE(w3.Intersects(w4));

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w5(tdb2, tdb4);
	ASSERT_FALSE(w2.Intersects(w5));
	ASSERT_FALSE(w5.Intersects(w2));

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w6(tdb, tdb4);
	ASSERT_TRUE(w3.Intersects(w6));
	ASSERT_TRUE(w6.Intersects(w3));

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w7(tdb3, tdb4);
	ASSERT_FALSE(w7.Intersects(w));
	ASSERT_FALSE(w.Intersects(w7));

	ASSERT_NE(w, w3);
}

TEST(Window, Merge)
{
	IO::Astrodynamics::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb2("2021-01-03 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb3("2021-01-05 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb4("2021-01-07 12:00:00 TDB");

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w(tdb, tdb3);
	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w2(tdb, tdb4);

	auto res = w.Merge(w2);

	ASSERT_DOUBLE_EQ(tdb.GetSecondsFromJ2000().count(),res.GetStartDate().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(tdb4.GetSecondsFromJ2000().count(),res.GetEndDate().GetSecondsFromJ2000().count());
}

TEST(Window, Merge2)
{
	IO::Astrodynamics::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb2("2021-01-03 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb3("2021-01-05 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb4("2021-01-07 12:00:00 TDB");

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w(tdb, tdb2);
	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w2(tdb3, tdb4);

	auto res = w.Merge(w2);

	ASSERT_DOUBLE_EQ(tdb.GetSecondsFromJ2000().count(),res.GetStartDate().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(tdb4.GetSecondsFromJ2000().count(),res.GetEndDate().GetSecondsFromJ2000().count());
}

TEST(Window, Merge3)
{
	IO::Astrodynamics::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb2("2021-01-03 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb3("2021-01-05 12:00:00 TDB");
	IO::Astrodynamics::Time::TDB tdb4("2021-01-07 12:00:00 TDB");

	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w(tdb, tdb2);
	IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> w2(tdb3, tdb4);

	auto res = w2.Merge(w);

	ASSERT_DOUBLE_EQ(tdb.GetSecondsFromJ2000().count(),res.GetStartDate().GetSecondsFromJ2000().count());
	ASSERT_DOUBLE_EQ(tdb4.GetSecondsFromJ2000().count(),res.GetEndDate().GetSecondsFromJ2000().count());
}