/**
 * @file Parameters.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef PARAMETERS_H
#define PARAMETERS_H
#include<string>

namespace IO::SDK::Parameters
{
	inline constexpr std::string_view KernelsPath = "Data/User/Spacecrafts";
	inline constexpr std::string_view SolarSystemKernelPath = "Data/SolarSystem";
	inline constexpr std::string_view SiteFramesPath = "Data/User/Sites/Frames";
	inline constexpr int CommentAreaSize = 5000;
	inline constexpr int MaximumEphemerisLagrangePolynomialDegree = 27;//[1-23]
	inline constexpr int MaximumOrientationLagrangePolynomialDegree = 23;//[1-23]

	inline constexpr double NodeDetectionAccuraccy = 0.0017453;//0.1°
	inline constexpr double IntersectDetectionAccuraccy = 0.017453;//1.0°
	inline constexpr double CircularEccentricityAccuraccy = 1E-03;
	inline constexpr double ClockAccuracy = 1.0/65536.0;

}
#endif