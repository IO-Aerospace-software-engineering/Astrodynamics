#ifndef ELLIPTICAL_INSTRUMENT_KERNEL_H
#define ELLIPTICAL_INSTRUMENT_KERNEL_H

#include<InstrumentKernel.h>
#include<Instrument.h>
#include<Vector3D.h>
#include<string>

namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Kernels
{
	class EllipticalInstrumentKernel final :public IO::SDK::Kernels::InstrumentKernel
	{
	private:
		double m_crossAngle{};

	protected:
		void BuildKernel() override;

	public:
		EllipticalInstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, const double angle, const double crossAngle);
		virtual ~EllipticalInstrumentKernel() = default;
		

	};
}

#endif
