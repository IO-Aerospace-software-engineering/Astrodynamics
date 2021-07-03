/**
 * @file SiteFramesLoader.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<SiteFramesLoader.h>
#include<filesystem>
#include<Parameters.h>
#include<SpiceUsr.h>

IO::SDK::Frames::SiteFramesLoader IO::SDK::Frames::SiteFramesLoader::m_instance{};

IO::SDK::Frames::SiteFramesLoader::SiteFramesLoader()
{
	if (!std::filesystem::exists(IO::SDK::Parameters::SiteFramesPath))
	{
		std::filesystem::create_directories(IO::SDK::Parameters::SiteFramesPath);
	}

	for (const auto& entry : std::filesystem::directory_iterator(IO::SDK::Parameters::SiteFramesPath))
	{
		furnsh_c(entry.path().string().c_str());
	}
}
