/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <UTC.h>
#include <SpiceUsr.h>
#include <SDKException.h>
#include <TDB.h>

IO::Astrodynamics::Time::UTC::UTC(const std::chrono::duration<double> ellapsedSecondsFromJ2000) : IO::Astrodynamics::Time::DateTime(ellapsedSecondsFromJ2000)
{
}

IO::Astrodynamics::Time::UTC::UTC(const std::string& string)
{
    SpiceDouble utc;
    SpiceChar errMsg[100];
    tparse_c(string.c_str(), 100, &utc, errMsg);

    if (*errMsg != '\000')
    {
        throw IO::Astrodynamics::Exception::SDKException(std::string(errMsg));
    }

    const_cast<std::chrono::duration<double>&>(m_secondsFromJ2000) = std::chrono::duration<double>(utc);
}

std::string IO::Astrodynamics::Time::UTC::ToString() const
{
    SpiceChar str[51];
    SpiceDouble delta;
    deltet_c(m_secondsFromJ2000.count(), "UTC", &delta);
    timout_c(m_secondsFromJ2000.count() + delta, "YYYY-MM-DD HR:MN:SC.###### (UTC) ::UTC", 51, str);
    return std::string{str};
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::Time::UTC::ToTDB() const
{
    double delta{};
    deltet_c(m_secondsFromJ2000.count(), "UTC", &delta);
    return TDB(m_secondsFromJ2000 + std::chrono::duration<double>(delta));
}

IO::Astrodynamics::Time::UTC IO::Astrodynamics::Time::UTC::Add(const IO::Astrodynamics::Time::TimeSpan &timespan) const
{
    return IO::Astrodynamics::Time::UTC{m_secondsFromJ2000 + timespan.GetSeconds()};
}

IO::Astrodynamics::Time::UTC IO::Astrodynamics::Time::UTC::operator+(const IO::Astrodynamics::Time::TimeSpan &timespan) const
{
    return Add(timespan);
}