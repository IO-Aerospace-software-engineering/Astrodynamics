/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <DateTime.h>
#include <sstream>

IO::Astrodynamics::Time::DateTime::DateTime()
= default;

IO::Astrodynamics::Time::DateTime::DateTime(const std::chrono::duration<double>& secondsFromJ2000) : m_secondsFromJ2000{secondsFromJ2000}
{
}

IO::Astrodynamics::Time::DateTime::DateTime(const IO::Astrodynamics::Time::DateTime &datetime) : DateTime(datetime.m_secondsFromJ2000)
{
}

std::chrono::duration<double> IO::Astrodynamics::Time::DateTime::GetSecondsFromJ2000() const
{
	return m_secondsFromJ2000;
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::DateTime::Subtract(const IO::Astrodynamics::Time::DateTime &other) const
{
	return IO::Astrodynamics::Time::TimeSpan{m_secondsFromJ2000 - other.m_secondsFromJ2000};
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::DateTime::operator-(const IO::Astrodynamics::Time::DateTime &other) const
{
	return Subtract(other);
}

bool IO::Astrodynamics::Time::DateTime::operator==(const IO::Astrodynamics::Time::DateTime &other) const
{
	return m_secondsFromJ2000 == other.m_secondsFromJ2000;
}

bool IO::Astrodynamics::Time::DateTime::operator!=(const IO::Astrodynamics::Time::DateTime &other) const
{
	return !(*this == other);
}

bool IO::Astrodynamics::Time::DateTime::operator>(const IO::Astrodynamics::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 > other.m_secondsFromJ2000;
}

bool IO::Astrodynamics::Time::DateTime::operator>=(const IO::Astrodynamics::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 >= other.m_secondsFromJ2000;
}

bool IO::Astrodynamics::Time::DateTime::operator<(const IO::Astrodynamics::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 < other.m_secondsFromJ2000;
}

bool IO::Astrodynamics::Time::DateTime::operator<=(const IO::Astrodynamics::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 <= other.m_secondsFromJ2000;
}

double IO::Astrodynamics::Time::DateTime::ToJulian() const
{
	return j2000_c() + m_secondsFromJ2000.count() / spd_c();
}

double IO::Astrodynamics::Time::DateTime::CenturiesFromJ2000() const
{
	return (ToJulian() - 2451545.0) / 36525;
}

IO::Astrodynamics::Time::DateTime &IO::Astrodynamics::Time::DateTime::operator=(const DateTime &datetime)
{
	// Guard self assignment
	if (this == &datetime)
		return *this;

	const_cast<std::chrono::duration<double> &>(m_secondsFromJ2000) = datetime.m_secondsFromJ2000;

	return *this;
}

void IO::Astrodynamics::Time::DateTime::ExtractDateTimeComponents(const std::string &dateTimeStr,
                                                             int &year, int &month, int &day,
                                                             int &hour, int &minute, double &second)
{
    // DateTime format: YYYY-MM-DD HR:MN:SC.###### (UTC)
    std::istringstream iss(dateTimeStr);
    char delim; // Used to ignore the delimiters (i.e., '-', ':', and spaces)

    // Parse the date (YYYY-MM-DD)
    iss >> year >> delim >> month >> delim >> day;

    // Parse the time (HR:MN:SC.######)
    iss >> hour >> delim >> minute >> delim >> second;
}