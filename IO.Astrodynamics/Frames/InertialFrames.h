/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INERTIAL_FRAMES_H
#define INERTIAL_FRAMES_H
#include <string>
#include <Frames.h>

namespace IO::Astrodynamics::Frames
{
	/**
	 * @brief Inertial frame
	 * 
	 */
	class InertialFrames final : public IO::Astrodynamics::Frames::Frames
	{

	private:
		static InertialFrames mICRF;
		static InertialFrames mECLIPTIC;
		static InertialFrames mGALACTIC;
	public:
		/**
		 * @brief Construct a new Inertial Frames object
		 * 
		 * @param name 
		 */
		explicit InertialFrames(const std::string &name);

		static IO::Astrodynamics::Frames::InertialFrames& GetICRF();
		static IO::Astrodynamics::Frames::InertialFrames& Galactic();
		static IO::Astrodynamics::Frames::InertialFrames& Ecliptic();
	};
}
#endif // ! INERTIAL_FRAMES_H