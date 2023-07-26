/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 26/07/2023.
//

#include <JulianDate.h>
#include <SpiceUsr.h>

IO::Astrodynamics::Time::JulianDate::JulianDate(std::chrono::duration<double, std::ratio<86400>> julianDate) : m_julianDate{julianDate}
{
    const_cast<std::chrono::duration<double> &>(m_secondsFromJ2000) = std::chrono::duration<double>(unitim_c(julianDate.count(), "JDTDB", "TDB"));
}

std::string IO::Astrodynamics::Time::JulianDate::ToString() const
{
    return std::to_string(m_julianDate.count()) + " JDTDB";
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
