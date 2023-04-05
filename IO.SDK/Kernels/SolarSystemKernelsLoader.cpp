/**
 * @file SolarSystemKernelsLoader.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "SolarSystemKernelsLoader.h"
#include<filesystem>
#include<Parameters.h>
#include<SpiceUsr.h>

IO::SDK::Kernels::SolarSystemKernelsLoader IO::SDK::Kernels::SolarSystemKernelsLoader::m_instance{};

IO::SDK::Kernels::SolarSystemKernelsLoader::SolarSystemKernelsLoader()
{
	if (!std::filesystem::exists(IO::SDK::Parameters::SolarSystemKernelPath))
	{
		std::filesystem::create_directories(IO::SDK::Parameters::SolarSystemKernelPath);
	}

	for (const auto& entry : std::filesystem::directory_iterator(IO::SDK::Parameters::SolarSystemKernelPath))
	{
		furnsh_c(entry.path().string().c_str());
	}
}
