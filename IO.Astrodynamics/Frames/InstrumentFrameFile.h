/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INSTRUMENT_FRAME_H
#define INSTRUMENT_FRAME_H

#include<FrameFile.h>
#include<Instrument.h>
#include<Vector3D.h>

namespace IO::Astrodynamics::Instruments
{
	class Instrument;
}

namespace IO::Astrodynamics::Frames
{
	/**
	 * @brief Instrument file frame
	 * 
	 */
	class InstrumentFrameFile final :public IO::Astrodynamics::Frames::FrameFile
	{

	private:
		/**
		 * @brief Construct a new Instrument Frame File object
		 * 
		 * @param instrument 
		 * @param orientation 
		 */
		InstrumentFrameFile(const IO::Astrodynamics::Instruments::Instrument& instrument, const IO::Astrodynamics::Math::Vector3D& orientation);
		const IO::Astrodynamics::Instruments::Instrument& m_instrument;
		const IO::Astrodynamics::Math::Vector3D m_orientation{};

		void BuildFrame() override;

	public:
		friend class IO::Astrodynamics::Instruments::Instrument;

	};
}
#endif // !INSTRUMENT_FRAME_H


