#ifndef FRAME_H
#define FRAME_H

#include<string>

namespace IO::SDK::Frames
{
	class FrameFile
	{
	private:
	protected:
		const std::string m_filePath{};
		bool m_fileExists{ false };
		bool m_isLoaded{ false };
		FrameFile(const std::string& filePath, const std::string& name);
		const std::string m_name{};

	public:
		virtual ~FrameFile();

		std::string GetFilePath();

		std::string GetName() const;
	};
}
#endif
