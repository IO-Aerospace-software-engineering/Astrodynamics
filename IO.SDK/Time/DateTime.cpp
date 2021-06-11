#include "DateTime.h"
#include <SDKException.h>

IO::SDK::Time::DateTime::DateTime()
{
}

IO::SDK::Time::DateTime::DateTime(std::chrono::duration<double> secondsFromJ2000) : m_secondsFromJ2000{secondsFromJ2000}
{
}

std::chrono::duration<double> IO::SDK::Time::DateTime::GetSecondsFromJ2000() const
{
	return m_secondsFromJ2000;
}

IO::SDK::Time::TimeSpan IO::SDK::Time::DateTime::Substract(const IO::SDK::Time::DateTime &other) const
{
	return IO::SDK::Time::TimeSpan(m_secondsFromJ2000 - other.m_secondsFromJ2000);
}

IO::SDK::Time::TimeSpan IO::SDK::Time::DateTime::operator-(const IO::SDK::Time::DateTime &other) const
{
	return Substract(other);
}

bool IO::SDK::Time::DateTime::operator==(const IO::SDK::Time::DateTime &other) const
{
	return m_secondsFromJ2000 == other.m_secondsFromJ2000;
}

bool IO::SDK::Time::DateTime::operator!=(const IO::SDK::Time::DateTime &other) const
{
	return !(*this == other);
}

bool IO::SDK::Time::DateTime::operator>(const IO::SDK::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 > other.m_secondsFromJ2000;
}

bool IO::SDK::Time::DateTime::operator>=(const IO::SDK::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 >= other.m_secondsFromJ2000;
}

bool IO::SDK::Time::DateTime::operator<(const IO::SDK::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 < other.m_secondsFromJ2000;
}

bool IO::SDK::Time::DateTime::operator<=(const IO::SDK::Time::DateTime &other) const
{
	return this->m_secondsFromJ2000 <= other.m_secondsFromJ2000;
}

double IO::SDK::Time::DateTime::ToJulian() const
{
	return j2000_c() + m_secondsFromJ2000.count() / spd_c();
}

double IO::SDK::Time::DateTime::CenturiesFromJ2000() const
{
	return (ToJulian() - 2451545.0) / 36525;
}

IO::SDK::Time::DateTime &IO::SDK::Time::DateTime::operator=(const DateTime &datetime)
{
	// Guard self assignment
	if (this == &datetime)
		return *this;

	m_secondsFromJ2000 = datetime.m_secondsFromJ2000;

	return *this;
}
