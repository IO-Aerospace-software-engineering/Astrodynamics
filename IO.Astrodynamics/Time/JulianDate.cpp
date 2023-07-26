//
// Created by s.guillet on 26/07/2023.
//

#include <JulianDate.h>
#include <SpiceUsr.h>

IO::Astrodynamics::Time::JulianDate::JulianDate(std::chrono::duration<double> julianDate) : m_julianDate{julianDate}
{
    const_cast<std::chrono::duration<double> &>(m_secondsFromJ2000) = std::chrono::duration<double>(unitim_c(julianDate.count(), "JDTDB", "TDB"));
}

std::string IO::Astrodynamics::Time::JulianDate::ToString() const
{
    SpiceChar str[51];
    timout_c(m_secondsFromJ2000.count(), "JULIAND.#### JDTDB", 51, str);
    return std::string{str};
}

IO::Astrodynamics::Time::JulianDate IO::Astrodynamics::Time::JulianDate::Add(const IO::Astrodynamics::Time::TimeSpan &timespan) const
{
    return IO::Astrodynamics::Time::JulianDate(m_julianDate + timespan.GetSeconds());
}

IO::Astrodynamics::Time::JulianDate IO::Astrodynamics::Time::JulianDate::operator+(const IO::Astrodynamics::Time::TimeSpan &timespan) const
{
    return Add(timespan);
}

IO::Astrodynamics::Time::JulianDate IO::Astrodynamics::Time::JulianDate::operator-(const IO::Astrodynamics::Time::TimeSpan &timespan) const
{
    return IO::Astrodynamics::Time::JulianDate(m_julianDate - timespan.GetSeconds());
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::JulianDate::operator-(const IO::Astrodynamics::Time::JulianDate &other) const
{
    return IO::Astrodynamics::Time::TimeSpan{m_julianDate - other.m_julianDate};
}

IO::Astrodynamics::Time::UTC IO::Astrodynamics::Time::JulianDate::ToUTC() const
{
    return IO::Astrodynamics::Time::TDB(m_secondsFromJ2000).ToUTC();
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::Time::JulianDate::ToTDB() const
{
    return IO::Astrodynamics::Time::TDB(m_secondsFromJ2000);
}
