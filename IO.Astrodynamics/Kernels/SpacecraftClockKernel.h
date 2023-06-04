/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SPACECRAFT_CLOCK_KERNEL_H
#define SPACECRAFT_CLOCK_KERNEL_H

#include <Kernel.h>
#include <Spacecraft.h>

namespace IO::Astrodynamics::Body::Spacecraft
{
	class Spacecraft;
}

namespace IO::Astrodynamics::Kernels
{
	class SpacecraftClockKernel final : public IO::Astrodynamics::Kernels::Kernel
	{
	private:
		/**
		 * @brief Build a generic clock kernel
		 * 
		 */
		void BuildGenericClockKernel();
		const IO::Astrodynamics::Body::Spacecraft::Spacecraft &m_spacecraft;
		const int m_resolution{};

		/**
		 * @brief Construct a new Spacecraft Clock Kernel object
		 * 
		 * @param spacecraft 
		 * @param resolution in bit 2^n
		 *
		 */
		SpacecraftClockKernel(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft, int resolution);

	public:
		~SpacecraftClockKernel() override = default;

		/**
		 * @brief Get the Coverage Window
		 * 
		 * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>
		 */
		[[nodiscard]] IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> GetCoverageWindow() const override;

		/**
		 * @brief Convert to TDB
		 * 
		 * @param clock 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] IO::Astrodynamics::Time::TDB ConvertToTDB(const std::string &clock) const;

		/**
		 * @brief Convert to TDB
		 * 
		 * @param encodedClock 
		 * @return IO::Astrodynamics::Time::TDB
		 */
		[[nodiscard]] IO::Astrodynamics::Time::TDB ConvertToTDB(double encodedClock) const;

		/**
		 * @brief Convert to Spacecraft clock
		 * 
		 * @param epoch 
		 * @return std::string 
		 */
		[[nodiscard]] std::string ConvertToClockString(const IO::Astrodynamics::Time::TDB &epoch) const;

		/**
		 * @brief Convert to enoded clock
		 * 
		 * @param epoch 
		 * @return double 
		 */
		[[nodiscard]] double ConvertToEncodedClock(const IO::Astrodynamics::Time::TDB &epoch) const;
		[[nodiscard]] static double ConvertToEncodedClock(int spacecraftId, const IO::Astrodynamics::Time::TDB &epoch);

		/**
		 * @brief Get the Resolution
		 * 
		 * @return int 
		 */
		[[nodiscard]] int GetResolution() const
		{
			return m_resolution;
		}

		/**
		 * @brief Get the Ticks Per Seconds
		 * 
		 * @return int 
		 */
		[[nodiscard]] int GetTicksPerSeconds() const;

		/**
		 * @brief Get the Seconds Per Tick
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetSecondsPerTick() const;



		friend class IO::Astrodynamics::Body::Spacecraft::Spacecraft;
	};
}

#endif // !SPACECRAFT_CLOCK_KERNEL_H
