#include<gtest/gtest.h>
#include<Window.h>
#include<TDB.h>
#include<chrono>

using namespace std::chrono_literals;

TEST(Window, Length)
{
	IO::SDK::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::SDK::Time::TDB tdb2("2021-01-03 12:00:00 TDB");

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w(tdb, tdb2);
	ASSERT_DOUBLE_EQ(48.0, w.GetLength().GetHours().count());
}

TEST(Window, EndDate)
{
	IO::SDK::Time::TDB tdb("2021-01-01 12:00:00 UTC");
	IO::SDK::Time::TimeSpan ts(72.0h);

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w(tdb, ts);
	ASSERT_STREQ("2021-01-04 12:01:09.183943 (TDB)", w.GetEndDate().ToString().c_str());
}

TEST(Window, Equals)
{
	IO::SDK::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::SDK::Time::TDB tdb2("2021-01-03 12:00:00 TDB");
	IO::SDK::Time::TDB tdb3("2021-01-05 12:00:00 TDB");

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w(tdb, tdb2);
	IO::SDK::Time::Window<IO::SDK::Time::TDB> w2(tdb, tdb2);
	ASSERT_EQ(w, w2);

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w3(tdb, tdb3);

	ASSERT_NE(w, w3);

}

TEST(Window, Intersects)
{
	IO::SDK::Time::TDB tdb("2021-01-01 12:00:00 TDB");
	IO::SDK::Time::TDB tdb2("2021-01-03 12:00:00 TDB");
	IO::SDK::Time::TDB tdb3("2021-01-05 12:00:00 TDB");
	IO::SDK::Time::TDB tdb4("2021-01-07 12:00:00 TDB");

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w(tdb, tdb2);
	IO::SDK::Time::Window<IO::SDK::Time::TDB> w2(tdb, tdb2);
	ASSERT_TRUE(w.ItIntersects(w2));
	ASSERT_TRUE(w2.ItIntersects(w));

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w3(tdb2, tdb3);
	ASSERT_FALSE(w2.ItIntersects(w3));
	ASSERT_FALSE(w3.ItIntersects(w2));

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w4(tdb, tdb3);
	ASSERT_TRUE(w4.ItIntersects(w3));
	ASSERT_TRUE(w3.ItIntersects(w4));

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w5(tdb2, tdb4);
	ASSERT_FALSE(w2.ItIntersects(w5));
	ASSERT_FALSE(w5.ItIntersects(w2));

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w6(tdb, tdb4);
	ASSERT_TRUE(w3.ItIntersects(w6));
	ASSERT_TRUE(w6.ItIntersects(w3));

	IO::SDK::Time::Window<IO::SDK::Time::TDB> w7(tdb3, tdb4);
	ASSERT_FALSE(w7.ItIntersects(w));
	ASSERT_FALSE(w.ItIntersects(w7));

	ASSERT_NE(w, w3);

}