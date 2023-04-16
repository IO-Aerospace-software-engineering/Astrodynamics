//
// Created by spacer on 4/14/23.
//

#ifndef IOSDKTESTS_PARAMETERS_H
#define IOSDKTESTS_PARAMETERS_H

#include <string>
#include <TimeSpan.h>

using namespace std::chrono_literals;


inline constexpr std::string_view SpacecraftPath = "Data/User/Spacecrafts";
inline constexpr std::string_view SolarSystemKernelPath = "Data/SolarSystem";
inline constexpr std::string_view SitePath = "Data/User/Sites";
inline constexpr int CommentAreaSize = 5000;
inline constexpr int MaximumEphemerisLagrangePolynomialDegreeEvenSpaced = 27;//[1-27]
inline constexpr int MaximumEphemerisLagrangePolynomialDegree = 15;//[1-27]

inline constexpr double NodeDetectionAccuraccy = 0.0017453;//0.1°
inline constexpr double IntersectDetectionAccuraccy = 0.017453;//1.0°
inline constexpr double CircularEccentricityAccuraccy = 1E-03;
inline constexpr double ClockAccuracy = 16.0; //2^n
inline const static IO::SDK::Time::TimeSpan SitePropagationStep(60s);
inline const static IO::SDK::Time::TimeSpan SpacecraftPropagationStep(1s);

#endif //IOSDKTESTS_PARAMETERS_H
