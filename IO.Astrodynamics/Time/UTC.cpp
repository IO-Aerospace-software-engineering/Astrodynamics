/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <UTC.h>
#include <SpiceUsr.h>
#include <SDKException.h>
#include <TDB.h>
#include <sstream>
#include "sofa.h"

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

void IO::Astrodynamics::Time::UTC::ConvertToJulianUTC_TT(const IO::Astrodynamics::Time::UTC &epoch,
                                                              double &jd_utc1, double &jd_utc2, double &jd_tt1,
                                                              double &jd_tt2)
{
    const auto utc = epoch.ToString();
    int year, month, day, hour, minute;
    double second;
    ExtractDateTimeComponents(utc, year, month, day, hour, minute, second);

    // Variables pour les Julian Dates
    double jd_tai1, jd_tai2;

    // Convertir la date UTC en Julian Date (jd_utc1 et jd_utc2)
    iauDtf2d("UTC", year, month, day, hour, minute, second, &jd_utc1, &jd_utc2);

    // Convertir UTC en TAI
    iauUtctai(jd_utc1, jd_utc2, &jd_tai1, &jd_tai2);

    // Convertir TAI en TT
    iauTaitt(jd_tai1, jd_tai2, &jd_tt1, &jd_tt2);
}

