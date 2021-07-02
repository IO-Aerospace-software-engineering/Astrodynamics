#ifndef SPACECRAFT_CLOCK_KERNEL_H
#define SPACECRAFT_CLOCK_KERNEL_H

#include<string>
#include<Kernel.h>
#include<TDB.h>
#include<Spacecraft.h>
#include<cmath>

namespace IO::SDK::Body::Spacecraft
{
	class Spacecraft;
}

namespace IO::SDK::Kernels
{
	class SpacecraftClockKernel final :public IO::SDK::Kernels::Kernel
	{
	private:
		void BuildGenericClockKernel();
		const IO::SDK::Body::Spacecraft::Spacecraft& m_spacecraft;
		const int m_resolution{};
		SpacecraftClockKernel(const IO::SDK::Body::Spacecraft::Spacecraft& spacecraft, const int resolution);

	public:

		virtual ~SpacecraftClockKernel() = default;

		/// <summary>
		/// Return partition window
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;

		/// <summary>
		/// Convert string clock to TDB
		/// </summary>
		/// <param name="clock">string clock with partition number</param>
		/// <returns></returns>
		IO::SDK::Time::TDB ConvertToTDB(const std::string& clock) const;

		/// <summary>
		/// Convert encoded clock to TDB
		/// </summary>
		/// <param name="encodedClock"></param>
		/// <returns></returns>
		IO::SDK::Time::TDB ConvertToTDB(const double encodedClock) const;

		/// <summary>
		/// Convert TDB to string clock
		/// </summary>
		/// <param name="epoch">TDB</param>
		/// <returns></returns>
		std::string ConvertToClock(const IO::SDK::Time::TDB& epoch) const;

		/// <summary>
		/// Convert TDB to continuous encoded spacecraft clock
		/// </summary>
		double ConvertToEncodedClock(const IO::SDK::Time::TDB& epoch) const;

		int GetResolution() const
		{
			return m_resolution;
		}

		/// <summary>
		/// Get ticks per seconds
		/// </summary>
		/// <returns></returns>
		int GetTicksPerSeconds() const;

		/// <summary>
		/// Get seconds per ticks
		/// </summary>
		/// <returns></returns>
		double GetSecondsPerTick()const ;

		friend class IO::SDK::Body::Spacecraft::Spacecraft;
	};
}

#endif // !SPACECRAFT_CLOCK_KERNEL_H


