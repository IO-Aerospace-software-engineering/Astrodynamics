#ifndef INSTRUMENT_KERNEL_H
#define INSTRUMENT_KERNEL_H

#include<string>
#include<Kernel.h>
#include<Instrument.h>
#include<Vector3D.h>
#include<TDB.h>
#include<Window.h>

namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Kernels
{
	class InstrumentKernel :public Kernel
	{
	private:


	protected:
		/// <summary>
		/// Instanciate instrument kernel
		/// </summary>
		/// <param name="instrument"></param>
		/// <param name="boresight"></param>
		/// <param name="refVector"></param>
		/// <param name="angle"></param>
		/// <param name="templateName"></param>
		InstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, const double angle, const std::string& templateName);
		const IO::SDK::Instruments::Instrument& m_instrument;
		const IO::SDK::Math::Vector3D m_boresight{};
		const IO::SDK::Math::Vector3D m_refVector{};
		const double m_angle{};
		const std::string m_templatePath;
		virtual void BuildKernel();

	public:

		virtual ~InstrumentKernel() = default;

		/// <summary>
		/// Get coverage window
		/// </summary>
		/// <returns></returns>
		virtual IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;

		friend class IO::SDK::Instruments::Instrument;

	};
}
#endif // !INSTRUMENT_KERNEL_H


