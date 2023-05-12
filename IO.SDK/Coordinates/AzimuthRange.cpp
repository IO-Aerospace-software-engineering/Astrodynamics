/**
 * @file AzimuthRange.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <AzimuthRange.h>
#include <Constants.h>
#include <cmath>

IO::SDK::Coordinates::AzimuthRange::AzimuthRange(const double start, const double end) : m_start{std::fmod(start, IO::SDK::Constants::_2PI)}, m_end{std::fmod(end, IO::SDK::Constants::_2PI)}
{
    if (m_start < 0.0)
    {
        const_cast<double &>(m_start) += IO::SDK::Constants::_2PI;
    }

    if (m_end < 0.0)
    {
        const_cast<double &>(m_end) += IO::SDK::Constants::_2PI;
    }

    //Set span
    const_cast<double &>(m_span) = m_end - m_start;
    if (m_span < 0.0)
    {
        const_cast<double &>(m_span) += IO::SDK::Constants::_2PI;
    }
}

bool IO::SDK::Coordinates::AzimuthRange::IsInRange(const double angle) const
{
    auto a = std::fmod(angle - m_start, IO::SDK::Constants::_2PI);
    if (a < 0.0)
    {
        a += IO::SDK::Constants::_2PI;
    }

    auto end = m_end;

    if (end < m_start)
    {
        end += IO::SDK::Constants::_2PI;
    }

    end = end - m_start;

    if (end < 0.0)
    {
        end += IO::SDK::Constants::_2PI;
    }

    if (a >= 0.0 && a <= end)
    {
        return true;
    }

    return false;
}

bool IO::SDK::Coordinates::AzimuthRange::IsIntersected(const AzimuthRange &azimuthRange) const
{
    return !(azimuthRange.m_start > m_end || azimuthRange.m_end < m_start);
}