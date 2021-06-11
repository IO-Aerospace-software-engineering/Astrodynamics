/**
 * @file UTC.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-05-17
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <UTC.h>
#include <SpiceUsr.h>
#include <SDKException.h>
#include <TDB.h>

IO::SDK::Time::UTC::UTC(std::chrono::duration<double> ellapsedSecondsFromJ2000) : IO::SDK::Time::DateTime(ellapsedSecondsFromJ2000)
{
}

IO::SDK::Time::UTC::UTC(std::string string)
{
    SpiceDouble utc;
    SpiceChar errMsg[100];
    tparse_c(string.c_str(), 100, &utc, errMsg);

    if (*errMsg != '\000')
    {
        throw IO::SDK::Exception::SDKException(std::string(errMsg));
    }

    m_secondsFromJ2000 = std::chrono::duration<double>(utc);
}

std::string IO::SDK::Time::UTC::ToString() const
{
    SpiceChar str[51];
    SpiceDouble delta;
    deltet_c(m_secondsFromJ2000.count(), "UTC", &delta);
    timout_c(m_secondsFromJ2000.count() + delta, "YYYY-MM-DD HR:MN:SC.###### (UTC) ::UTC", 51, str);
    return std::string(str);
}

IO::SDK::Time::TDB IO::SDK::Time::UTC::ToTDB() const
{
	double delta{};
	deltet_c(m_secondsFromJ2000.count(), "UTC", &delta);
	return TDB(m_secondsFromJ2000 + std::chrono::duration<double>(delta));
}

IO::SDK::Time::UTC IO::SDK::Time::UTC::Add(const IO::SDK::Time::TimeSpan &timespan) const
{
	return IO::SDK::Time::UTC(m_secondsFromJ2000 + timespan.GetSeconds());
}

IO::SDK::Time::UTC IO::SDK::Time::UTC::operator+(const IO::SDK::Time::TimeSpan &timespan) const
{
	return Add(timespan);
}