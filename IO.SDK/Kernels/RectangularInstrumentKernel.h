#ifndef RECTANGULAR_INSTRUMENT_KERNEL_H
#define RECTANGULAR_INSTRUMENT_KERNEL_H

#include<InstrumentKernel.h>
#include<Instrument.h>
#include<Vector3D.h>

namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Kernels
{
	class RectangularInstrumentKernel final :public IO::SDK::Kernels::InstrumentKernel
	{
	private:
		double m_crossAngle{};
		
	protected:
		void BuildKernel() override;

	public:
		RectangularInstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, const double angle, const double crossAngle);
		virtual ~RectangularInstrumentKernel() = default;
	};
}

#endif
