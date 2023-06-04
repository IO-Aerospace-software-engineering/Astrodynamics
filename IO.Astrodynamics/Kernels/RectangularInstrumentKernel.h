/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef RECTANGULAR_INSTRUMENT_KERNEL_H
#define RECTANGULAR_INSTRUMENT_KERNEL_H

#include<Instrument.h>

namespace IO::Astrodynamics::Instruments
{
	class Instrument;
}

namespace IO::Astrodynamics::Kernels
{
	class RectangularInstrumentKernel final :public IO::Astrodynamics::Kernels::InstrumentKernel
	{
	private:
		double m_crossAngle{};
		
	protected:
		/**
		 * @brief Build kernel
		 * 
		 */
		void BuildKernel() override;

	public:
		/**
		 * @brief Construct a new Rectangular Instrument Kernel object
		 * 
		 * @param instrument 
		 * @param boresight 
		 * @param refVector 
		 * @param angle 
		 * @param crossAngle 
		 */
		RectangularInstrumentKernel(const IO::Astrodynamics::Instruments::Instrument& instrument, const IO::Astrodynamics::Math::Vector3D& boresight, const IO::Astrodynamics::Math::Vector3D& refVector, double angle, double crossAngle);
		~RectangularInstrumentKernel() override = default;
	};
}

#endif
