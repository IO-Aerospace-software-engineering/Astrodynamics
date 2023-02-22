/**
 * @file EphemerisKernel.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef EPHEMERIS_KERNEL_H
#define EPHEMERIS_KERNEL_H
#include <string>
#include <vector>
#include <memory>

#include <Kernel.h>
#include <StateVector.h>
#include <Aberrations.h>
#include <CelestialBody.h>
#include <TDB.h>
#include <InertialFrames.h>
#include <Site.h>
#include <Spacecraft.h>
#include <Window.h>
#include <InvalidArgumentException.h>


namespace IO::SDK::Kernels
{
	/**
	 * @brief Ephemeris kernel
	 * 
	 */
	class EphemerisKernel final : public Kernel
	{
	private:
		const int m_objectId;
		bool IsEvenlySpacedData(const std::vector<OrbitalParameters::StateVector> &states) const;

	public:
		/**
		 * @brief Construct a new Ephemeris Kernel object
		 * 
		 * @param spacecraft
		 */
		EphemerisKernel(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);
		EphemerisKernel(const IO::SDK::Sites::Site &site);
		virtual ~EphemerisKernel() = default;

		/**
		 * @brief 
		 * 
		 * @param observer 
		 * @param frame 
		 * @param aberration 
		 * @param epoch 
		 * @return IO::SDK::OrbitalParameters::StateVector 
		 */
		IO::SDK::OrbitalParameters::StateVector ReadStateVector(const IO::SDK::Body::CelestialBody &observer, const IO::SDK::Frames::Frames& frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const;
		/**
		 * @brief Get the Coverage Window
		 * 
		 * @return IO::SDK::Time::Window<IO::SDK::Time::TDB> 
		 */
		IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;

		/**
		 * @brief Write date to ephemeris file
		 * 
		 * @param states 
		 */
		void WriteData(const std::vector<OrbitalParameters::StateVector> &states);
	};
}
#endif
