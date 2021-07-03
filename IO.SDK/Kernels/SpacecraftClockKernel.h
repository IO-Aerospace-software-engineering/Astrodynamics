/**
 * @file SpacecraftClockKernel.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SPACECRAFT_CLOCK_KERNEL_H
#define SPACECRAFT_CLOCK_KERNEL_H

#include <string>
#include <Kernel.h>
#include <TDB.h>
#include <Spacecraft.h>
#include <cmath>

namespace IO::SDK::Body::Spacecraft
{
	class Spacecraft;
}

namespace IO::SDK::Kernels
{
	class SpacecraftClockKernel final : public IO::SDK::Kernels::Kernel
	{
	private:
		/**
		 * @brief Build a generic clock kernel
		 * 
		 */
		void BuildGenericClockKernel();
		const IO::SDK::Body::Spacecraft::Spacecraft &m_spacecraft;
		const int m_resolution{};

		/**
		 * @brief Construct a new Spacecraft Clock Kernel object
		 * 
		 * @param spacecraft 
		 * @param resolution 
		 */
		SpacecraftClockKernel(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, const int resolution);

	public:
		virtual ~SpacecraftClockKernel() = default;

		/**
		 * @brief Get the Coverage Window
		 * 
		 * @return IO::SDK::Time::Window<IO::SDK::Time::TDB> 
		 */
		IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;

		/**
		 * @brief Convert to TDB
		 * 
		 * @param clock 
		 * @return IO::SDK::Time::TDB 
		 */
		IO::SDK::Time::TDB ConvertToTDB(const std::string &clock) const;

		/**
		 * @brief Convert to TDB
		 * 
		 * @param encodedClock 
		 * @return IO::SDK::Time::TDB 
		 */
		IO::SDK::Time::TDB ConvertToTDB(const double encodedClock) const;

		/**
		 * @brief Convert to spacecraft clock
		 * 
		 * @param epoch 
		 * @return std::string 
		 */
		std::string ConvertToClockString(const IO::SDK::Time::TDB &epoch) const;

		/**
		 * @brief Convert to enoded clock
		 * 
		 * @param epoch 
		 * @return double 
		 */
		double ConvertToEncodedClock(const IO::SDK::Time::TDB &epoch) const;

		/**
		 * @brief Get the Resolution
		 * 
		 * @return int 
		 */
		int GetResolution() const
		{
			return m_resolution;
		}

		/**
		 * @brief Get the Ticks Per Seconds
		 * 
		 * @return int 
		 */
		int GetTicksPerSeconds() const;

		/**
		 * @brief Get the Seconds Per Tick
		 * 
		 * @return double 
		 */
		double GetSecondsPerTick() const;

		friend class IO::SDK::Body::Spacecraft::Spacecraft;
	};
}

#endif // !SPACECRAFT_CLOCK_KERNEL_H
