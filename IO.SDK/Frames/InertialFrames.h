/**
 * @file InertialFrames.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef INERTIAL_FRAMES_H
#define INERTIAL_FRAMES_H
#include <string>
#include <Frames.h>

namespace IO::SDK::Frames
{
	/**
	 * @brief Inertial frame
	 * 
	 */
	class InertialFrames final : public IO::SDK::Frames::Frames
	{

	private:
	public:

	/**
	 * @brief Construct a new Inertial Frames object
	 * 
	 * @param name 
	 */
	InertialFrames(const std::string& name);
	static InertialFrames ICRF;
	static InertialFrames ECLIPTIC;
	static InertialFrames GALACTIC;

	
	};
}
#endif // ! INERTIAL_FRAMES_H