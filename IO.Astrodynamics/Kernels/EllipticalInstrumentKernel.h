/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef ELLIPTICAL_INSTRUMENT_KERNEL_H
#define ELLIPTICAL_INSTRUMENT_KERNEL_H

#include <InstrumentKernel.h>
#include <Instrument.h>
#include <Vector3D.h>
#include <string>

namespace IO::Astrodynamics::Instruments
{
	class Instrument;
}

namespace IO::Astrodynamics::Kernels
{
	/**
	 * @brief Elliptical instrument kernel
	 * 
	 */
	class EllipticalInstrumentKernel final : public IO::Astrodynamics::Kernels::InstrumentKernel
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
		EllipticalInstrumentKernel(const IO::Astrodynamics::Instruments::Instrument &instrument, const IO::Astrodynamics::Math::Vector3D &boresight, const IO::Astrodynamics::Math::Vector3D &refVector, double angle, double crossAngle);
		~EllipticalInstrumentKernel() override = default;
	};
}

#endif
