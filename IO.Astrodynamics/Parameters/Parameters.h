/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef PARAMETERS_H
#define PARAMETERS_H

#include <TimeSpan.h>

using namespace std::chrono_literals;

namespace IO::SDK::Parameters
{

    inline constexpr int CommentAreaSize = 5000;
    inline constexpr int MaximumEphemerisLagrangePolynomialDegreeEvenSpaced = 27;//[1-27]
    inline constexpr int MaximumEphemerisLagrangePolynomialDegree = 15;//[1-27]

    inline constexpr double NodeDetectionAccuraccy = 0.0017453;//0.1°
    inline constexpr double IntersectDetectionAccuraccy = 0.017453;//1.0°
    inline constexpr double CircularEccentricityAccuraccy = 1E-03;
    inline constexpr double ClockAccuracy = 16.0; //2^n
    inline const static Time::TimeSpan SitePropagationStep(60s);
    inline const static Time::TimeSpan SpacecraftPropagationStep(1s);

}
#endif