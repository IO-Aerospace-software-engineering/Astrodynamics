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
		CircularInstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, const double angle);

	};
}

#endif
