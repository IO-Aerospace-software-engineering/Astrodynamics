#ifndef EPHEMERIS_KERNEL_H
#define EPHEMERIS_KERNEL_H
#include <string>
#include <vector>
#include <memory>

#include <SpiceUsr.h>
#include <Kernel.h>
#include <StateVector.h>
#include <Aberrations.h>
#include <CelestialBody.h>
#include <TDB.h>
#include <InertialFrames.h>
#include <Spacecraft.h>
#include <Window.h>
#include <InvalidArgumentException.h>

namespace IO::SDK::Kernels
{
	class EphemerisKernel final : public Kernel
	{
	private:
		const IO::SDK::Body::Spacecraft::Spacecraft &m_spacecraft;
		bool IsEvenlySpacedData(const std::vector<OrbitalParameters::StateVector> &states) const;

	public:
		EphemerisKernel(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);
		virtual ~EphemerisKernel() = default;

		IO::SDK::OrbitalParameters::StateVector ReadStateVector(const IO::SDK::Body::CelestialBody &observer, const IO::SDK::Frames::Frames& frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &epoch) const;
		IO::SDK::Time::Window<IO::SDK::Time::TDB> GetCoverageWindow() const override;
		void WriteData(const std::vector<OrbitalParameters::StateVector> &states);
	};
}
#endif
