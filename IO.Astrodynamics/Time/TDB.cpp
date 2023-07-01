/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <TDB.h>
#include <UTC.h>

IO::Astrodynamics::Time::TDB::TDB(const std::chrono::duration<double> ellapsedSecondsFromJ2000) : IO::Astrodynamics::Time::DateTime(ellapsedSecondsFromJ2000)
{
}

IO::Astrodynamics::Time::TDB::TDB(const std::string& string)
{
	SpiceDouble tdb;
	str2et_c(string.c_str(), &tdb);
	const_cast<std::chrono::duration<double>&>(m_secondsFromJ2000) = std::chrono::duration<double>(tdb);
}

std::string IO::Astrodynamics::Time::TDB::ToString() const
{
	SpiceChar str[51];
	timout_c(m_secondsFromJ2000.count(), "YYYY-MM-DD HR:MN:SC.###### (TDB) ::TDB", 51, str);
	return std::string{str};
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::Time::TDB::Add(const IO::Astrodynamics::Time::TimeSpan &timespan) const
{
	return IO::Astrodynamics::Time::TDB{m_secondsFromJ2000 + timespan.GetSeconds()};
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::Time::TDB::operator+(const IO::Astrodynamics::Time::TimeSpan &timespan) const
{
	return Add(timespan);
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::Time::TDB::operator-(const IO::Astrodynamics::Time::TimeSpan &other) const
{
	return IO::Astrodynamics::Time::TDB{m_secondsFromJ2000 - other.GetSeconds()};
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::TDB::operator-(const IO::Astrodynamics::Time::TDB &other) const
{
	return Subtract(other);
}

IO::Astrodynamics::Time::UTC IO::Astrodynamics::Time::TDB::ToUTC() const
{
	double delta{};
	deltet_c(m_secondsFromJ2000.count(), "et", &delta);
	return UTC{m_secondsFromJ2000 - std::chrono::duration<double>(delta)};
}