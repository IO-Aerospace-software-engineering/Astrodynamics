/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef EPHEMERIS_KERNEL_H
#define EPHEMERIS_KERNEL_H

#include <Kernel.h>
#include <StateVector.h>
#include <Spacecraft.h>


namespace IO::Astrodynamics::Kernels {
    /**
     * @brief Ephemeris kernel
     *
     */
    class EphemerisKernel final : public Kernel {
    private:
        const int m_objectId;

        static bool IsEvenlySpacedData(const std::vector<OrbitalParameters::StateVector> &states);

    public:
        /**
         * @brief Construct a new Ephemeris Kernel object
         *
         * @param spacecraft
         */
        explicit EphemerisKernel(std::string filePath, int objectId);

        ~EphemerisKernel() override = default;

        /**
         * @brief
         *
         * @param observer
         * @param frame
         * @param aberration
         * @param epoch
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        [[nodiscard]] IO::Astrodynamics::OrbitalParameters::StateVector
        ReadStateVector(const IO::Astrodynamics::Body::CelestialBody &observer, const IO::Astrodynamics::Frames::Frames &frame, IO::Astrodynamics::AberrationsEnum aberration,
                        const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Get the Coverage Window
         *
         * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>
         */
        [[nodiscard]] IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> GetCoverageWindow() const override;

        /**
         * @brief Write date to ephemeris file
         *
         * @param states
         */
        void WriteData(const std::vector<OrbitalParameters::StateVector> &states);
    };
}
#endif
