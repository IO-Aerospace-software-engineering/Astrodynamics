/**
 * @file FrameFile.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <FrameFile.h>
#include<filesystem>
#include<SpiceUsr.h>

IO::SDK::Frames::FrameFile::FrameFile(const std::string& filePath, const std::string& name) : m_filePath{ filePath }, m_name{ name }
{
	auto directory = std::filesystem::directory_entry(filePath).path();
	if (directory.has_parent_path())
	{
		if (!std::filesystem::exists(directory.parent_path()))
		{
			std::filesystem::create_directories(directory.parent_path());
		}
	}

	if (std::filesystem::is_block_file(m_filePath) && std::filesystem::exists(m_filePath))
	{
		m_fileExists = true;
		furnsh_c(m_filePath.c_str());
		m_isLoaded = true;
	}
}

IO::SDK::Frames::FrameFile::~FrameFile()
{
	unload_c(m_filePath.c_str());
}

std::string IO::SDK::Frames::FrameFile::GetName() const
{
	return m_name;
}
