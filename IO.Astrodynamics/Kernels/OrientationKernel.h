/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef ORIENTATION_KERNEL_H
#define ORIENTATION_KERNEL_H

#include<Kernel.h>
#include<vector>
#include<StateOrientation.h>

//Forward declaration
namespace IO::Astrodynamics::Body::Spacecraft
{
    class Spacecraft;
}

namespace IO::Astrodynamics::Kernels
{
    class OrientationKernel final : public IO::Astrodynamics::Kernels::Kernel
    {
    private:
        int m_spacecraftId{};
        int m_spacecraftFrameId{};


    public:
        /**
         * @brief Construct a new Orientation Kernel object
         *
         * @param spacecraft
         */
        OrientationKernel(std::string filePath, int spacecraftId, int spacecraftFrameId);

        ~OrientationKernel() override;

        /**
         * @brief Write orientations data
         *
         * @param orientations
         */
        void WriteOrientations(const std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> &orientations);

        /**
         * @brief Get the Coverage Window
         *
         * @return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>
         */
        [[nodiscard]] IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> GetCoverageWindow() const override;

        /**
         * @brief Read state orientations
         *
         * @param epoch
         * @param tolerance
         * @param frame
         * @return IO::Astrodynamics::OrbitalParameters::StateOrientation
         */
        [[nodiscard]] IO::Astrodynamics::OrbitalParameters::StateOrientation
        ReadStateOrientation(const Body::Spacecraft::Spacecraft &spacecraft, const IO::Astrodynamics::Time::TDB &epoch, const IO::Astrodynamics::Time::TimeSpan &tolerance,
                             const IO::Astrodynamics::Frames::Frames &frame) const;

        friend class IO::Astrodynamics::Body::Spacecraft::Spacecraft;
    };
}
#endif // !ORIENTATION_KERNEL_H





