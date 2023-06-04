/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INSTRUMENT_FRAME_H
#define INSTRUMENT_FRAME_H

#include<FrameFile.h>
#include<Instrument.h>
#include<Vector3D.h>

namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Frames
{
	/**
	 * @brief Instrument file frame
	 * 
	 */
	class InstrumentFrameFile final :public IO::SDK::Frames::FrameFile
	{

	private:
		/**
		 * @brief Construct a new Instrument Frame File object
		 * 
		 * @param instrument 
		 * @param orientation 
		 */
		InstrumentFrameFile(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& orientation);
		const IO::SDK::Instruments::Instrument& m_instrument;
		const IO::SDK::Math::Vector3D m_orientation{};

		void BuildFrame();

	public:
		friend class IO::SDK::Instruments::Instrument;

	};
}
#endif // !INSTRUMENT_FRAME_H


