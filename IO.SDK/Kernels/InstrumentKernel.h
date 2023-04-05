/**
 * @file InstrumentKernel.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef INSTRUMENT_KERNEL_H
#define INSTRUMENT_KERNEL_H

#include<Instrument.h>

namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Kernels
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
		InstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, double angle);
		const IO::SDK::Instruments::Instrument& m_instrument;
		const IO::SDK::Math::Vector3D m_boresight{};
		const IO::SDK::Math::Vector3D m_refVector{};
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
		 * @return IO::SDK::Time::Window<IO::SDK::Time::TDB> 
		 */
		[[nodiscard]] IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;

		friend class IO::SDK::Instruments::Instrument;

	};
}
#endif // !INSTRUMENT_KERNEL_H


