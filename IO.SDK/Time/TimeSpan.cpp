/**
 * @file TimeSpan.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "TimeSpan.h"

IO::SDK::Time::TimeSpan::TimeSpan()
= default;

IO::SDK::Time::TimeSpan::TimeSpan(const std::chrono::duration<double> seconds) : m_seconds{seconds}
{
}

std::chrono::duration<double, std::nano> IO::SDK::Time::TimeSpan::GetNanoseconds() const
{
    return std::chrono::duration<double, std::nano>{m_seconds};
}

std::chrono::duration<double, std::micro> IO::SDK::Time::TimeSpan::GetMicroseconds() const
{
    return std::chrono::duration<double, std::micro>{m_seconds};
}

std::chrono::duration<double, std::milli> IO::SDK::Time::TimeSpan::GetMilliseconds() const
{
    return std::chrono::duration<double, std::milli>{m_seconds};
}

std::chrono::duration<double> IO::SDK::Time::TimeSpan::GetSeconds() const
{
    return m_seconds;
}

std::chrono::duration<double, std::ratio<60>> IO::SDK::Time::TimeSpan::GetMinutes() const
{
    return std::chrono::duration<double, std::ratio<60>>{m_seconds};
}

std::chrono::duration<double, std::ratio<3600>> IO::SDK::Time::TimeSpan::GetHours() const
{
    return std::chrono::duration<double, std::ratio<3600>>{m_seconds};
}

IO::SDK::Time::TimeSpan IO::SDK::Time::TimeSpan::operator+(const IO::SDK::Time::TimeSpan &ts) const
{
    return IO::SDK::Time::TimeSpan{m_seconds + ts.m_seconds};
}

IO::SDK::Time::TimeSpan IO::SDK::Time::TimeSpan::operator+(const double val) const
{
    return IO::SDK::Time::TimeSpan{m_seconds + std::chrono::duration<double>(val)};
}

IO::SDK::Time::TimeSpan IO::SDK::Time::TimeSpan::operator-(const IO::SDK::Time::TimeSpan &ts) const
{
    return IO::SDK::Time::TimeSpan{m_seconds - ts.m_seconds};
}

IO::SDK::Time::TimeSpan IO::SDK::Time::TimeSpan::operator*(const double value) const
{
    return IO::SDK::Time::TimeSpan{m_seconds * value};
}

IO::SDK::Time::TimeSpan IO::SDK::Time::TimeSpan::operator/(const double value) const
{
    return IO::SDK::Time::TimeSpan{m_seconds / value};
}

bool IO::SDK::Time::TimeSpan::operator==(const IO::SDK::Time::TimeSpan &ts) const
{
    return m_seconds == ts.m_seconds;
}

bool IO::SDK::Time::TimeSpan::operator!=(const IO::SDK::Time::TimeSpan &ts) const
{
    return !(m_seconds == ts.m_seconds);
}

bool IO::SDK::Time::TimeSpan::operator>(const IO::SDK::Time::TimeSpan &ts) const
{
    return m_seconds > ts.m_seconds;
}

bool IO::SDK::Time::TimeSpan::operator>=(const IO::SDK::Time::TimeSpan &ts) const
{
    return m_seconds >= ts.m_seconds;
}

bool IO::SDK::Time::TimeSpan::operator<(const IO::SDK::Time::TimeSpan &ts) const
{
    return m_seconds < ts.m_seconds;
}

bool IO::SDK::Time::TimeSpan::operator<=(const IO::SDK::Time::TimeSpan &ts) const
{
    return m_seconds <= ts.m_seconds;
}