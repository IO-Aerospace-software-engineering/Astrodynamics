/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <FrameFile.h>
#include<filesystem>
#include <utility>
#include<SpiceUsr.h>

IO::Astrodynamics::Frames::FrameFile::FrameFile(const std::string& filePath, std::string  name) : m_filePath{ filePath }, m_name{std::move( name )}
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
        std::filesystem::remove(m_filePath);
	}
}

IO::Astrodynamics::Frames::FrameFile::~FrameFile()
{
	unload_c(m_filePath.c_str());
}

std::string IO::Astrodynamics::Frames::FrameFile::GetName() const
{
	return m_name;
}
