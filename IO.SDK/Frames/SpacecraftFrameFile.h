#ifndef SPACECRAFT_FRAME_H
#define SPACECRAFT_FRAME_H

#include <string>
#include <Spacecraft.h>
#include <FrameFile.h>

//Forward declaration
namespace IO::SDK::Body::Spacecraft
{
	class Spacecraft;
}

namespace IO::SDK::Frames
{
	class SpacecraftFrameFile final : public IO::SDK::Frames::FrameFile
	{
	private:
		const int m_id;
		const IO::SDK::Body::Spacecraft::Spacecraft &m_spacecraft;
		void BuildFrame();
		SpacecraftFrameFile(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

	public:
		friend class IO::SDK::Body::Spacecraft::Spacecraft;
	};
}
#endif
