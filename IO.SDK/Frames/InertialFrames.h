#ifndef INERTIAL_FRAMES_H
#define INERTIAL_FRAMES_H
#include <string>
#include <Frames.h>

namespace IO::SDK::Frames
{

	class InertialFrames : public IO::SDK::Frames::Frames
	{

	private:
	public:
	InertialFrames(const std::string& name);
	static InertialFrames ICRF;
	static InertialFrames ECLIPTIC;
	static InertialFrames GALACTIC;

	
	};
}
#endif // ! INERTIAL_FRAMES_H