/**
 * @file EllipticalInstrumentKernel.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef ELLIPTICAL_INSTRUMENT_KERNEL_H
#define ELLIPTICAL_INSTRUMENT_KERNEL_H

#include <InstrumentKernel.h>
#include <Instrument.h>
#include <Vector3D.h>
#include <string>

namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Kernels
{
	/**
	 * @brief Elliptical instrument kernel
	 * 
	 */
	class EllipticalInstrumentKernel final : public IO::SDK::Kernels::InstrumentKernel
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
		 * @brief Construct a new Elliptical Instrument Kernel object
		 * 
		 * @param instrument 
		 * @param boresight 
		 * @param refVector 
		 * @param angle 
		 * @param crossAngle 
		 */
		EllipticalInstrumentKernel(const IO::SDK::Instruments::Instrument &instrument, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &refVector, double angle, double crossAngle);
		~EllipticalInstrumentKernel() override = default;
	};
}

#endif
