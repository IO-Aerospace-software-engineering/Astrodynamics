#ifndef PARAMETERS_H
#define PARAMETERS_H
#include<string>

namespace IO::SDK::Parameters
{
	inline constexpr std::string_view KernelsPath = "Data";
	inline constexpr std::string_view KernelTemplates = "Templates";
	inline constexpr std::string_view SolarSystemKernelPath = "Data/SolarSystem";
	inline constexpr std::string_view SiteFramesPath = "Data/Sites/Frames";
	inline constexpr int CommentAreaSize = 5000;
	inline constexpr int LagrangePolynomialDegree = 9;//[1-27]
	inline constexpr double NodeDetectionAccuraccy = 0.0017453;//0.1°
	inline constexpr double IntersectDetectionAccuraccy = 0.017453;//1.0°
	inline constexpr double CircularEccentricityAccuraccy = 1E-03;

}
#endif