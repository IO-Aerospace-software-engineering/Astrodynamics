/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef FRAME_H
#define FRAME_H

#include<string>

namespace IO::SDK::Frames
{
	/**
	 * @brief Frame file
	 * 
	 */
	class FrameFile
	{
	private:
	protected:
		const std::string m_filePath{};
		bool m_fileExists{ false };
		bool m_isLoaded{ false };
		FrameFile(const std::string& filePath, std::string  name);
		const std::string m_name{};

	public:
		virtual ~FrameFile();

		/**
		 * @brief Get the frame name
		 * 
		 * @return std::string 
		 */
		[[nodiscard]] std::string GetName() const;
	};
}
#endif
