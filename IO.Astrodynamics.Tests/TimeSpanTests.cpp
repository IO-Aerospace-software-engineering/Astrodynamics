#include <gtest/gtest.h>
#include <TimeSpan.h>
#include <chrono>
using namespace std::literals::chrono_literals;

TEST(TimeSpan, Initialization)
{
	IO::Astrodynamics::Time::TimeSpan ts(0.278543188965147h);

	ASSERT_DOUBLE_EQ(1002.7554802745293, ts.GetSeconds().count());
}

TEST(TimeSpan, GetNanoseconds)
{
	IO::Astrodynamics::Time::TimeSpan ts(0.278543188965147h);

	ASSERT_DOUBLE_EQ(1002755480274.5293, ts.GetNanoseconds().count());
}

TEST(TimeSpan, GetMicroseconds)
{
	IO::Astrodynamics::Time::TimeSpan ts(0.278543188965147h);

	ASSERT_DOUBLE_EQ(1002755480.2745293, ts.GetMicroseconds().count());
}

TEST(TimeSpan, GetMilliseconds)
{
	IO::Astrodynamics::Time::TimeSpan ts(0.278543188965147h);

	ASSERT_DOUBLE_EQ(1002755.4802745293, ts.GetMilliseconds().count());
}

TEST(TimeSpan, GetSeconds)
{
	IO::Astrodynamics::Time::TimeSpan ts(0.278543188965147h);

	ASSERT_DOUBLE_EQ(1002.7554802745293, ts.GetSeconds().count());
}

TEST(TimeSpan, GetMinutes)
{
	IO::Astrodynamics::Time::TimeSpan ts(0.278543188965147h);

	ASSERT_DOUBLE_EQ(16.712591337908822, ts.GetMinutes().count());
}

TEST(TimeSpan, GetHours)
{
	IO::Astrodynamics::Time::TimeSpan ts(0.278543188965147h);

	ASSERT_DOUBLE_EQ(0.278543188965147, ts.GetHours().count());
}

TEST(TimeSpan, Add)
{
	IO::Astrodynamics::Time::TimeSpan ts(1h);
	IO::Astrodynamics::Time::TimeSpan ts2(30min);

	ASSERT_DOUBLE_EQ(1.5, (ts + ts2).GetHours().count());
}

TEST(TimeSpan, Substract)
{
	IO::Astrodynamics::Time::TimeSpan ts(1h);
	IO::Astrodynamics::Time::TimeSpan ts2(30min);

	ASSERT_DOUBLE_EQ(0.5, (ts - ts2).GetHours().count());
}

TEST(TimeSpan, Multiply)
{
	IO::Astrodynamics::Time::TimeSpan ts(1h);

	ASSERT_DOUBLE_EQ(3.0, (ts * 3).GetHours().count());
}

TEST(TimeSpan, Divide)
{
	IO::Astrodynamics::Time::TimeSpan ts(1h);

	ASSERT_DOUBLE_EQ(0.5, (ts / 2).GetHours().count());
}

TEST(TimeSpan, Equals)
{
	IO::Astrodynamics::Time::TimeSpan ts1(1h);
	IO::Astrodynamics::Time::TimeSpan ts2(1h);

	ASSERT_EQ(ts1, ts2);
}
