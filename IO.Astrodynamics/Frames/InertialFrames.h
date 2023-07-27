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
		static InertialFrames mECLIPTIC_J2000;
		static InertialFrames mECLIPTIC_B1950;
		static InertialFrames mGALACTIC;
		static InertialFrames mB1950;
		static InertialFrames mFK4;
	public:
		/**
		 * @brief Construct a new Inertial Frames object
		 * 
		 * @param name 
		 */
		explicit InertialFrames(const std::string &name);

		static IO::Astrodynamics::Frames::InertialFrames& ICRF();
		static IO::Astrodynamics::Frames::InertialFrames& Galactic();
		static IO::Astrodynamics::Frames::InertialFrames& EclipticJ2000();
		static IO::Astrodynamics::Frames::InertialFrames& EclipticB1950();
		static IO::Astrodynamics::Frames::InertialFrames& B1950();
		static IO::Astrodynamics::Frames::InertialFrames& FK4();
	};
}
#endif // ! INERTIAL_FRAMES_H