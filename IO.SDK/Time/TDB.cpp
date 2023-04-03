/**
 * @file TDB.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-05-17
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <TDB.h>
#include <UTC.h>

IO::SDK::Time::TDB::TDB(const std::chrono::duration<double> ellapsedSecondsFromJ2000) : IO::SDK::Time::DateTime(ellapsedSecondsFromJ2000)
{
}

IO::SDK::Time::TDB::TDB(const std::string& string)
{
	SpiceDouble tdb;
	str2et_c(string.c_str(), &tdb);
	const_cast<std::chrono::duration<double>&>(m_secondsFromJ2000) = std::chrono::duration<double>(tdb);
}

std::string IO::SDK::Time::TDB::ToString() const
{
	SpiceChar str[51];
	timout_c(m_secondsFromJ2000.count(), "YYYY-MM-DD HR:MN:SC.###### (TDB) ::TDB", 51, str);
	return std::string{str};
}

IO::SDK::Time::TDB IO::SDK::Time::TDB::Add(const IO::SDK::Time::TimeSpan &timespan) const
{
	return IO::SDK::Time::TDB{m_secondsFromJ2000 + timespan.GetSeconds()};
}

IO::SDK::Time::TDB IO::SDK::Time::TDB::operator+(const IO::SDK::Time::TimeSpan &timespan) const
{
	return Add(timespan);
}

IO::SDK::Time::TDB IO::SDK::Time::TDB::operator-(const IO::SDK::Time::TimeSpan &other) const
{
	return IO::SDK::Time::TDB{m_secondsFromJ2000 - other.GetSeconds()};
}

IO::SDK::Time::TimeSpan IO::SDK::Time::TDB::operator-(const IO::SDK::Time::TDB &other) const
{
	return Subtract(other);
}

IO::SDK::Time::UTC IO::SDK::Time::TDB::ToUTC() const
{
	double delta{};
	deltet_c(m_secondsFromJ2000.count(), "et", &delta);
	return UTC{m_secondsFromJ2000 - std::chrono::duration<double>(delta)};
}