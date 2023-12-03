/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 12/1/23.
//

#ifndef IO_TOOLS_H
#define IO_TOOLS_H

#include <Constants.h>
#include <math.h>

inline static double AngleDifference(double angleA, double angleB)
{
    double delta = std::fmod(angleA - angleB + IO::Astrodynamics::Constants::_2PI,IO::Astrodynamics::Constants::_2PI);
    if (delta > IO::Astrodynamics::Constants::PI)
    {
        delta = IO::Astrodynamics::Constants::_2PI - delta;
    } else if (delta < -IO::Astrodynamics::Constants::PI)
    {
        delta = IO::Astrodynamics::Constants::_2PI + delta;
    }
    return std::abs(delta);
}

#endif //IO_TOOLS_H
