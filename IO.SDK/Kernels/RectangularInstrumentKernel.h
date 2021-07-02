/**
 * @file RectangularInstrumentKernel.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
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
		/**
		 * @brief 
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
		RectangularInstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, const double angle, const double crossAngle);
		virtual ~RectangularInstrumentKernel() = default;
	};
}

#endif
