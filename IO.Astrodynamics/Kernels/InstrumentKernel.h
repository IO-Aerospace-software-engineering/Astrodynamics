/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INSTRUMENT_KERNEL_H
#define INSTRUMENT_KERNEL_H

#include<Instrument.h>

namespace IO::Astrodynamics::Instruments
{
	class Instrument;
}

namespace IO::Astrodynamics::Kernels
{
	/**
	 * @brief Instrument kernel
	 * 
	 */
	class InstrumentKernel :public Kernel
	{
	private:


	protected:
		/**
		 * @brief Construct a new Instrument Kernel object
		 * 
		 * @param instrument 
		 * @param boresight 
		 * @param refVector 
		 * @param angle 
		 * @param templateName 
		 */
		InstrumentKernel(const IO::Astrodynamics::Instruments::Instrument& instrument, const IO::Astrodynamics::Math::Vector3D& boresight, const IO::Astrodynamics::Math::Vector3D& refVector, double angle);
		const IO::Astrodynamics::Instruments::Instrument& m_instrument;
		const IO::Astrodynamics::Math::Vector3D m_boresight{};
		const IO::Astrodynamics::Math::Vector3D m_refVector{};
		const double m_angle{};

		/**
		 * @brief Build kernel
		 * 
		 */
		virtual void BuildKernel();

	public:

		~InstrumentKernel() override = default;

		/**
		 * @brief Get the Coverage Window
		 * 
		 * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>
		 */
		[[nodiscard]] IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> GetCoverageWindow() const override;

		friend class IO::Astrodynamics::Instruments::Instrument;

	};
}
#endif // !INSTRUMENT_KERNEL_H


