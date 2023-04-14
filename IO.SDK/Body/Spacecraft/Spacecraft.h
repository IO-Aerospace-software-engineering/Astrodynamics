/**
 * @file Spacecraft.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-03-04
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SPACECRAFT_H
#define SPACECRAFT_H

#include <SpacecraftFrameFile.h>
#include <SpacecraftClockKernel.h>
#include <OrientationKernel.h>
#include <EphemerisKernel.h>
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
    class Spacecraft final : public Body
    {
    private:
        const std::string m_filesPath{};
        const std::unique_ptr<IO::SDK::Frames::SpacecraftFrameFile> m_frame;
        const std::unique_ptr<IO::SDK::Kernels::SpacecraftClockKernel> m_clockKernel;
        const std::unique_ptr<IO::SDK::Kernels::OrientationKernel> m_orientationKernel;
        const std::unique_ptr<IO::SDK::Kernels::EphemerisKernel> m_ephemerisKernel;
        std::vector<std::unique_ptr<IO::SDK::Instruments::Instrument>> m_instruments{};
        std::vector<std::unique_ptr<IO::SDK::Body::Spacecraft::FuelTank>> m_fuelTanks{};
        std::vector<std::unique_ptr<IO::SDK::Body::Spacecraft::Engine>> m_engines{};
        std::vector<std::unique_ptr<IO::SDK::Body::Spacecraft::Payload>> m_payloads{};

        bool HasInstrument(unsigned short id);

        const double m_maximumOperatingMass;

    public:

        const IO::SDK::Math::Vector3D Top;
        const IO::SDK::Math::Vector3D Front;
        const IO::SDK::Math::Vector3D Right;
        const IO::SDK::Math::Vector3D Bottom;
        const IO::SDK::Math::Vector3D Back;
        const IO::SDK::Math::Vector3D Left;

        /**
         * @brief Construct a new Spacecraft object
         *
         * @param id
         * @param name
         * @param dryOperatingMass
         * @param maximumOperatingMass
         * @param directoryPath
         * @param orbitalParametersAtEpoch
         * @param attitudeAtEpoch
         */
        Spacecraft(int id, const std::string &name, double dryOperatingMass, double maximumOperatingMass,
                   const std::string &directoryPath,
                   std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch);

        /**
         * Construct a new Spacecraft object
         * @param id Naif identifier (must be a negative number)
         * @param name
         * @param dryOperatingMass
         * @param maximumOperatingMass
         * @param directoryPath Mission name
         * @param orbitalParametersAtEpoch Original orbital parameters
         * @param front Front vector relative to ICRF
         * @param top Top vector relative to ICRF
         */
        Spacecraft(int id, const std::string &name, double dryOperatingMass, double maximumOperatingMass,
                   std::string directoryPath,
                   std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch, const IO::SDK::Math::Vector3D &front,
                   const IO::SDK::Math::Vector3D &top);

        Spacecraft(const Spacecraft &spacecraft) = delete;

        ~Spacecraft() override = default;

        /**
         * @brief Get the Files Path object
         *
         * @return std::string
         */
        inline std::string GetFilesPath() const
        { return m_filesPath; }

        /**
         * @brief
         *
         * @param orientations
         */
        void WriteOrientations(
                const std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> &orientations) const;

        /**
         * @brief Get the Orientation object
         *
         * @param epoch Orientation at epoch
         * @param tolerance Tolerance after and before epoch
         * @param frame Frame in wich orienation will be returned
         * @return IO::SDK::OrbitalParameters::StateOrientation
         */
        IO::SDK::OrbitalParameters::StateOrientation
        GetOrientation(const IO::SDK::Time::TDB &epoch, const IO::SDK::Time::TimeSpan &tolerance,
                       const IO::SDK::Frames::Frames &frame) const;

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
        void WriteEphemeris(const std::vector<OrbitalParameters::StateVector> &states) const;

        /**
         * @brief Get the Statevector object
         *
         * @param observer
         * @param frame
         * @param aberration
         * @param tdb
         * @return IO::SDK::OrbitalParameters::StateVector
         */
        IO::SDK::OrbitalParameters::StateVector
        ReadEphemeris(const IO::SDK::Frames::Frames &frame, IO::SDK::AberrationsEnum aberration,
                      const IO::SDK::Time::TDB &tdb, const IO::SDK::Body::CelestialBody &observer) const override;

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
        void AddCircularFOVInstrument(unsigned short id, const std::string &name,
                                      const IO::SDK::Math::Vector3D &orientation,
                                      const IO::SDK::Math::Vector3D &boresight,
                                      const IO::SDK::Math::Vector3D &fovRefVector, double fovAngle);

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
        void AddRectangularFOVInstrument(unsigned short id, const std::string &name,
                                         const IO::SDK::Math::Vector3D &orientation,
                                         const IO::SDK::Math::Vector3D &boresight,
                                         const IO::SDK::Math::Vector3D &fovRefVector, double fovAngle,
                                         double crossAngle);

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
        void AddEllipticalFOVInstrument(unsigned short id, const std::string &name,
                                        const IO::SDK::Math::Vector3D &orientation,
                                        const IO::SDK::Math::Vector3D &boresight,
                                        const IO::SDK::Math::Vector3D &fovRefVector, double fovAngle,
                                        double crossAngle);

        /**
         * @brief Get the Instrument object
         *
         * @param id
         * @return const IO::SDK::Instruments::Instrument*
         */
        const IO::SDK::Instruments::Instrument *GetInstrument(int id) const;

        /**
         * @brief Add a fuel tank
         *
         * @param id
         * @param capacity
         * @param quantity
         */
        void AddFuelTank(const std::string &serialNumber, double capacity, double quantity);

        /**
         * @brief Add an engine
         *
         * @param id
         * @param serialNumber
         * @param name
         * @param fuelTankSerialNumber
         * @param position
         * @param orientation
         * @param isp
         * @param fuelFlow
         */
        void
        AddEngine(const std::string &serialNumber, const std::string &name, const std::string &fuelTankSerialNumber,
                  const Math::Vector3D &position, const Math::Vector3D &orientation, double isp,
                  double fuelFlow);

        /**
         * @brief Add a payload
         *
         * @param serialNumber
         * @param name
         * @param mass
         */
        void AddPayload(const std::string &serialNumber, const std::string &name, double mass);

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

        /**
         * Get associated frame
         * @return frame
         */
        const std::unique_ptr<IO::SDK::Frames::SpacecraftFrameFile> &GetFrame() const;

        inline double GetMaximumOperatingMass()
        { return m_maximumOperatingMass; }
    };
}

#endif // !SPACECRAFT_H
