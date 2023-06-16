/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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
namespace IO::Astrodynamics::Instruments
{
    class Instrument;
}

namespace IO::Astrodynamics::Frames
{
    class SpacecraftFrameFile;
}

namespace IO::Astrodynamics::Kernels
{
    class SpacecraftClockKernel;

    class OrientationKernel;

    class EphemerisKernel;
}

namespace IO::Astrodynamics::Body::Spacecraft
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
        const std::unique_ptr<IO::Astrodynamics::Frames::SpacecraftFrameFile> m_frame;
        const std::unique_ptr<IO::Astrodynamics::Kernels::SpacecraftClockKernel> m_clockKernel;
        const std::unique_ptr<IO::Astrodynamics::Kernels::OrientationKernel> m_orientationKernel;
        const std::unique_ptr<IO::Astrodynamics::Kernels::EphemerisKernel> m_ephemerisKernel;
        std::vector<std::unique_ptr<IO::Astrodynamics::Instruments::Instrument>> m_instruments{};
        std::vector<std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::FuelTank>> m_fuelTanks{};
        std::vector<std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::Engine>> m_engines{};
        std::vector<std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::Payload>> m_payloads{};

        bool HasInstrument(int id);

        const double m_maximumOperatingMass;

    public:

        const IO::Astrodynamics::Math::Vector3D Top;
        const IO::Astrodynamics::Math::Vector3D Front;
        const IO::Astrodynamics::Math::Vector3D Right;
        const IO::Astrodynamics::Math::Vector3D Bottom;
        const IO::Astrodynamics::Math::Vector3D Back;
        const IO::Astrodynamics::Math::Vector3D Left;

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
                   std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch);

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
                   std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch, const IO::Astrodynamics::Math::Vector3D &front,
                   const IO::Astrodynamics::Math::Vector3D &top);

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
                const std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> &orientations) const;

        /**
         * @brief Get the Orientation object
         *
         * @param epoch Orientation at epoch
         * @param tolerance Tolerance after and before epoch
         * @param frame Frame in wich orienation will be returned
         * @return IO::Astrodynamics::OrbitalParameters::StateOrientation
         */
        IO::Astrodynamics::OrbitalParameters::StateOrientation
        GetOrientation(const IO::Astrodynamics::Time::TDB &epoch, const IO::Astrodynamics::Time::TimeSpan &tolerance,
                       const IO::Astrodynamics::Frames::Frames &frame) const;

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
         * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>
         */
        IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> GetOrientationsCoverageWindow() const;

        /**
         * @brief Get the Clock object
         *
         * @return const IO::Astrodynamics::Kernels::SpacecraftClockKernel&
         */
        const IO::Astrodynamics::Kernels::SpacecraftClockKernel &GetClock() const;

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
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        IO::Astrodynamics::OrbitalParameters::StateVector
        ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame, IO::Astrodynamics::AberrationsEnum aberration,
                      const IO::Astrodynamics::Time::TDB &tdb, const IO::Astrodynamics::Body::CelestialBody &observer) const override;

        /**
         * @brief Get the Ephemeris Coverage Window object
         *
         * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>
         */
        IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> GetEphemerisCoverageWindow() const;

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
        void AddCircularFOVInstrument(int id, const std::string &name,
                                      const IO::Astrodynamics::Math::Vector3D &orientation,
                                      const IO::Astrodynamics::Math::Vector3D &boresight,
                                      const IO::Astrodynamics::Math::Vector3D &fovRefVector, double fovAngle);

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
        void AddRectangularFOVInstrument(int id, const std::string &name,
                                         const IO::Astrodynamics::Math::Vector3D &orientation,
                                         const IO::Astrodynamics::Math::Vector3D &boresight,
                                         const IO::Astrodynamics::Math::Vector3D &fovRefVector, double fovAngle,
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
        void AddEllipticalFOVInstrument(int id, const std::string &name,
                                        const IO::Astrodynamics::Math::Vector3D &orientation,
                                        const IO::Astrodynamics::Math::Vector3D &boresight,
                                        const IO::Astrodynamics::Math::Vector3D &fovRefVector, double fovAngle,
                                        double crossAngle);

        /**
         * @brief Get the Instrument object
         *
         * @param id
         * @return const IO::Astrodynamics::Instruments::Instrument*
         */
        const IO::Astrodynamics::Instruments::Instrument *GetInstrument(int id) const;

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
         * @return IO::Astrodynamics::Body::Spacecraft::Engine*
         */
        const IO::Astrodynamics::Body::Spacecraft::Engine *GetEngine(const std::string &serialNumber) const;

        /**
         * @brief Get the Fueltank object
         *
         * @param serialNumber
         * @return IO::Astrodynamics::Body::Spacecraft::FuelTank*
         */
        IO::Astrodynamics::Body::Spacecraft::FuelTank *GetFueltank(const std::string &serialNumber) const;

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
        const std::unique_ptr<IO::Astrodynamics::Frames::SpacecraftFrameFile> &GetFrame() const;

        inline double GetMaximumOperatingMass() const
        { return m_maximumOperatingMass; }
    };
}

#endif // !SPACECRAFT_H
