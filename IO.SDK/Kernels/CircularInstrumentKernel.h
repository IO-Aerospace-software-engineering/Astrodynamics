/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef CIRCULAR_INSTRUMENT_KERNEL_H
#define CIRCULAR_INSTRUMENT_KERNEL_H

#include<InstrumentKernel.h>
#include<Instrument.h>
#include<Vector3D.h>

namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Kernels
{
	class CircularInstrumentKernel final :public IO::SDK::Kernels::InstrumentKernel
	{
	private:

	public:
		/**
		 * @brief Construct a new Circular Instrument Kernel object
		 * 
		 * @param instrument 
		 * @param boresight 
		 * @param refVector 
		 * @param angle 
		 */
		CircularInstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, double angle);

	};
}

#endif
