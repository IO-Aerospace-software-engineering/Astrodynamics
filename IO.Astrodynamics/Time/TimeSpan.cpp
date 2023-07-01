/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <TimeSpan.h>

IO::Astrodynamics::Time::TimeSpan::TimeSpan()
= default;

IO::Astrodynamics::Time::TimeSpan::TimeSpan(const std::chrono::duration<double> seconds) : m_seconds{seconds}
{
}

IO::Astrodynamics::Time::TimeSpan::TimeSpan(double period) : TimeSpan(std::chrono::duration<double>(period))
{

}

std::chrono::duration<double, std::nano> IO::Astrodynamics::Time::TimeSpan::GetNanoseconds() const
{
    return std::chrono::duration<double, std::nano>{m_seconds};
}

std::chrono::duration<double, std::micro> IO::Astrodynamics::Time::TimeSpan::GetMicroseconds() const
{
    return std::chrono::duration<double, std::micro>{m_seconds};
}

std::chrono::duration<double, std::milli> IO::Astrodynamics::Time::TimeSpan::GetMilliseconds() const
{
    return std::chrono::duration<double, std::milli>{m_seconds};
}

std::chrono::duration<double> IO::Astrodynamics::Time::TimeSpan::GetSeconds() const
{
    return m_seconds;
}

std::chrono::duration<double, std::ratio<60>> IO::Astrodynamics::Time::TimeSpan::GetMinutes() const
{
    return std::chrono::duration<double, std::ratio<60>>{m_seconds};
}

std::chrono::duration<double, std::ratio<3600>> IO::Astrodynamics::Time::TimeSpan::GetHours() const
{
    return std::chrono::duration<double, std::ratio<3600>>{m_seconds};
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::TimeSpan::operator+(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return IO::Astrodynamics::Time::TimeSpan{m_seconds + ts.m_seconds};
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::TimeSpan::operator+(const double val) const
{
    return IO::Astrodynamics::Time::TimeSpan{m_seconds + std::chrono::duration<double>(val)};
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::TimeSpan::operator-(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return IO::Astrodynamics::Time::TimeSpan{m_seconds - ts.m_seconds};
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::TimeSpan::operator*(const double value) const
{
    return IO::Astrodynamics::Time::TimeSpan{m_seconds * value};
}

IO::Astrodynamics::Time::TimeSpan IO::Astrodynamics::Time::TimeSpan::operator/(const double value) const
{
    return IO::Astrodynamics::Time::TimeSpan{m_seconds / value};
}

bool IO::Astrodynamics::Time::TimeSpan::operator==(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return m_seconds == ts.m_seconds;
}

bool IO::Astrodynamics::Time::TimeSpan::operator!=(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return !(m_seconds == ts.m_seconds);
}

bool IO::Astrodynamics::Time::TimeSpan::operator>(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return m_seconds > ts.m_seconds;
}

bool IO::Astrodynamics::Time::TimeSpan::operator>=(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return m_seconds >= ts.m_seconds;
}

bool IO::Astrodynamics::Time::TimeSpan::operator<(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return m_seconds < ts.m_seconds;
}

bool IO::Astrodynamics::Time::TimeSpan::operator<=(const IO::Astrodynamics::Time::TimeSpan &ts) const
{
    return m_seconds <= ts.m_seconds;
}


