/**
 * @file Spacecraft.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-04
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SPACECRAFT_H
#define SPACECRAFT_H

#include <string>
#include <memory>
#include <vector>

#include <Body.h>
#include <SpacecraftFrameFile.h>
#include <SpacecraftClockKernel.h>
#include <OrientationKernel.h>
#include <StateOrientation.h>
#include <EphemerisKernel.h>
#include <InertialFrames.h>
#include <TDB.h>
#include <Window.h>
#include <StateVector.h>
#include <Aberrations.h>
#include <Vector3D.h>
#include <Instrument.h>
#include <FuelTank.h>
#include <Payload.h>
#include <Engine.h>
#include <InstrumentFrameFile.h>

//Forward declaration
namespace IO::SDK::Instruments
{
	class Instrument;
}

namespace IO::SDK::Frames
{
	class SpacecraftFrameFile;
}

namespace IO::SDK::Kernels
{
	class SpacecraftClockKernel;
	class OrientationKernel;
	class EphemerisKernel;
}

namespace IO::SDK::Body::Spacecraft
{
	class Engine;
	/**
	 * @brief Spacecraft class
	 * 
	 */
	class Spacecraft : public Body
	{
	private:
		const std::string m_missionPrefix{};
		const std::string m_filesPath{};
		const std::unique_ptr<IO::SDK::Kernels::SpacecraftClockKernel> m_clockKernel;
		const std::unique_ptr<IO::SDK::Kernels::OrientationKernel> m_orientationKernel;
		const std::unique_ptr<IO::SDK::Frames::SpacecraftFrameFile> m_frame;
		const std::unique_ptr<IO::SDK::Kernels::EphemerisKernel> m_ephemerisKernel;
		std::vector<std::unique_ptr<IO::SDK::Instruments::Instrument>> m_instruments{};
		std::vector<std::unique_ptr<IO::SDK::Body::Spacecraft::FuelTank>> m_fuelTanks{};
		std::vector<std::unique_ptr<IO::SDK::Body::Spacecraft::Engine>> m_engines{};
		std::vector<std::unique_ptr<IO::SDK::Body::Spacecraft::Payload>> m_payloads{};
		bool HasInstrument(unsigned short id);
		const double m_maximumOperatingMass;

	public:
		//Orientation
		const IO::SDK::Math::Vector3D Up{0.0, 0.0, 1.0};
		const IO::SDK::Math::Vector3D Front{0.0, 1.0, 0.0};
		const IO::SDK::Math::Vector3D Left{-1.0, 0.0, 0.0};

		/**
		 * @brief Construct a new Spacecraft object
		 * 
		 * @param id 
		 * @param name 
		 * @param dryOperatingMass 
		 * @param maximumOperatingMass 
		 * @param missionPrefix 
		 * @param orbitalParametersAtEpoch 
		 * @param attitudeAtEpoch 
		 */
		Spacecraft(const int id, const std::string &name, const double dryOperatingMass, double maximumOperatingMass, const std::string &missionPrefix, std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch);
		Spacecraft(const Spacecraft &spacecraft) = delete;
		virtual ~Spacecraft() override = default;

		/**
		 * @brief Get the Mission Prefix object
		 * 
		 * @return std::string 
		 */
		std::string GetMissionPrefix() const;

		/**
		 * @brief Get the Files Path object
		 * 
		 * @return std::string 
		 */
		std::string GetFilesPath() const;

		/**
		 * @brief 
		 * 
		 * @param orientations 
		 */
		void WriteOrientations(const std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> &orientations) const;

		/**
		 * @brief Get the Orientation object
		 * 
		 * @param epoch Orientation at epoch
		 * @param tolerance Tolerance after and before epoch
		 * @param frame Frame in wich orienation will be returned
		 * @return IO::SDK::OrbitalParameters::StateOrientation 
		 */
		IO::SDK::OrbitalParameters::StateOrientation GetOrientation(const IO::SDK::Time::TDB &epoch, const IO::SDK::Time::TimeSpan &tolerance, const IO::SDK::Frames::Frames &frame) const;

		/**
		 * @brief Write comment in orientation kernel
		 * 
		 * @param comment 
		 */
		void WriteOrientationKernelComment(const std::string &comment) const;

		/**
		 * @brief Read orientation kernel comment
		 * 
		 * @return std::string 
		 */
		std::string ReadOrientationKernelComment() const;

		/**
		 * @brief Get the Orientations Coverage Window object
		 * 
		 * @return IO::SDK::Time::Window<IO::SDK::Time::TDB> 
		 */
		IO::SDK::Time::Window<IO::SDK::Time::TDB> GetOrientationsCoverageWindow() const;

		/**
		 * @brief Get the Clock object
		 * 
		 * @return const IO::SDK::Kernels::SpacecraftClockKernel& 
		 */
		const IO::SDK::Kernels::SpacecraftClockKernel &GetClock() const;

		/**
		 * @brief Write ephemeris data
		 * 
		 * @param states 
		 * @param frame 
		 */
		void WriteEphemeris(const std::vector<OrbitalParameters::StateVector> &states, IO::SDK::Frames::Frames &frame) const;

		/**
		 * @brief Get the Statevector object
		 * 
		 * @param observer 
		 * @param frame 
		 * @param aberration 
		 * @param tdb 
		 * @return IO::SDK::OrbitalParameters::StateVector 
		 */
		IO::SDK::OrbitalParameters::StateVector ReadEphemeris( const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &tdb,const IO::SDK::Body::CelestialBody &observer) const override;

		/**
		 * @brief Get the Ephemeris Coverage Window object
		 * 
		 * @return IO::SDK::Time::Window<IO::SDK::Time::TDB> 
		 */
		IO::SDK::Time::Window<IO::SDK::Time::TDB> GetEphemerisCoverageWindow() const;

		/**
		 * @brief Write a comment in ephemeris kernel
		 * 
		 * @param comment 
		 */
		void WriteEphemerisKernelComment(const std::string &comment) const;

		/**
		 * @brief Read ephemeris kernel comment
		 * 
		 * @return std::string 
		 */
		std::string ReadEphemerisKernelComment() const;

		/**
		 * @brief Add instrument with a circular field of view
		 * 
		 * @param id 
		 * @param name 
		 * @param orientation 
		 * @param boresight 
		 * @param fovRefVector 
		 * @param fovAngle 
		 */
		void AddCircularFOVInstrument(const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle);

		/**
		 * @brief Add an instrument with a rectangular field of view
		 * 
		 * @param id 
		 * @param name 
		 * @param orientation 
		 * @param boresight 
		 * @param fovRefVector 
		 * @param fovAngle 
		 * @param crossAngle 
		 */
		void AddRectangularFOVInstrument(const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle, const double crossAngle);

		/**
		 * @brief Add an instrument with an elliptical field of view
		 * 
		 * @param id 
		 * @param name 
		 * @param orientation 
		 * @param boresight 
		 * @param fovRefVector 
		 * @param fovAngle 
		 * @param crossAngle 
		 */
		void AddEllipticalFOVInstrument(const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle, const double crossAngle);

		/**
		 * @brief Get the Instrument object
		 * 
		 * @param id 
		 * @return const IO::SDK::Instruments::Instrument* 
		 */
		const IO::SDK::Instruments::Instrument *GetInstrument(const int id) const;

		/**
		 * @brief Add a fuel tank
		 * 
		 * @param id 
		 * @param capacity 
		 * @param quantity 
		 */
		void AddFuelTank(const std::string &serialNumber, const double capacity, const double quantity);

		/**
		 * @brief Add an engine
		 * 
		 * @param id 
		 * @param serialNumber 
		 * @param name 
		 * @param fueltank 
		 * @param position 
		 * @param orientation 
		 * @param isp 
		 * @param fuelFlow 
		 */
		void AddEngine(const std::string &serialNumber, const std::string &name, const std::string &fuelTankSerialNumber, const Math::Vector3D &position, const Math::Vector3D &orientation, const double isp, const double fuelFlow);

		/**
		 * @brief Add a payload
		 * 
		 * @param serialNumber 
		 * @param name 
		 * @param mass 
		 */
		void AddPayload(const std::string &serialNumber, const std::string &name, const double mass);

		/**
		 * @brief Get the toal mass
		 * 
		 * @return double 
		 */
		double GetMass() const override;

		/**
		 * @brief Get the Engine object
		 * 
		 * @param serialNumber 
		 * @return IO::SDK::Body::Spacecraft::Engine* 
		 */
		const IO::SDK::Body::Spacecraft::Engine *GetEngine(const std::string &serialNumber) const;

		/**
		 * @brief Get the Fueltank object
		 * 
		 * @param serialNumber 
		 * @return IO::SDK::Body::Spacecraft::FuelTank* 
		 */
		IO::SDK::Body::Spacecraft::FuelTank *GetFueltank(const std::string &serialNumber) const;

		/**
		 * @brief Release a payload
		 * 
		 * @param serialNumber 
		 */
		void ReleasePayload(const std::string &serialNumber);

		/**
		 * @brief Get the Dry Operating Mass object
		 * 
		 * @return double 
		 */
		double GetDryOperatingMass() const;
	};
}

#endif // !SPACECRAFT_H
