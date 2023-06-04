/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <AzimuthRange.h>
#include <Constants.h>
#include <cmath>

IO::Astrodynamics::Coordinates::AzimuthRange::AzimuthRange(const double start, const double end) : m_start{std::fmod(start, IO::Astrodynamics::Constants::_2PI)}, m_end{std::fmod(end, IO::Astrodynamics::Constants::_2PI)}
{
    if (m_start < 0.0)
    {
        const_cast<double &>(m_start) += IO::Astrodynamics::Constants::_2PI;
    }

    if (m_end < 0.0)
    {
        const_cast<double &>(m_end) += IO::Astrodynamics::Constants::_2PI;
    }

    //Set span
    const_cast<double &>(m_span) = m_end - m_start;
    if (m_span < 0.0)
    {
        const_cast<double &>(m_span) += IO::Astrodynamics::Constants::_2PI;
    }
}

bool IO::Astrodynamics::Coordinates::AzimuthRange::IsInRange(const double angle) const
{
    auto a = std::fmod(angle - m_start, IO::Astrodynamics::Constants::_2PI);
    if (a < 0.0)
    {
        a += IO::Astrodynamics::Constants::_2PI;
    }

    auto end = m_end;

    if (end < m_start)
    {
        end += IO::Astrodynamics::Constants::_2PI;
    }

    end = end - m_start;

    if (end < 0.0)
    {
        end += IO::Astrodynamics::Constants::_2PI;
    }

    if (a >= 0.0 && a <= end)
    {
        return true;
    }

    return false;
}

bool IO::Astrodynamics::Coordinates::AzimuthRange::IsIntersected(const AzimuthRange &azimuthRange) const
{
    return !(azimuthRange.m_start > m_end || azimuthRange.m_end < m_start);
}