/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef INSTRUMENT_H
#define INSTRUMENT_H

#include <Spacecraft.h>
#include <FOVShapes.h>
#include <InstrumentKernel.h>
#include <Site.h>

namespace IO::Astrodynamics::Body::Spacecraft
{
    class Spacecraft;
}

namespace IO::Astrodynamics::Frames
{
    class InstrumentFrameFile;
}

namespace IO::Astrodynamics::Kernels
{

    class CircularInstrumentKernel;

    class RectangularInstrumentKernel;

    class EllipticalInstrumentKernel;

    class InstrumentKernel;
}

namespace IO::Astrodynamics::Instruments
{
    class Instrument
    {
    private:
        const IO::Astrodynamics::Body::Spacecraft::Spacecraft &m_spacecraft;
        const int m_id{};
        const std::string m_name;
        const std::string m_filesPath;
        const std::unique_ptr<IO::Astrodynamics::Frames::InstrumentFrameFile> m_frame{nullptr};
        const IO::Astrodynamics::Math::Vector3D m_orientation{};
        const IO::Astrodynamics::Instruments::FOVShapeEnum m_fovShape{};
        const IO::Astrodynamics::Math::Vector3D m_boresight{};
        const IO::Astrodynamics::Math::Vector3D m_fovRefVector{};
        const std::unique_ptr<IO::Astrodynamics::Kernels::InstrumentKernel> m_kernel{nullptr};

        /**
         * @brief Construct a new circular instrument object
         *
         * @param spacecraft
         * @param id
         * @param name
         * @param orientation
         * @param boresight
         * @param fovRefVector
         * @param fovAngle
         */
        Instrument(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft, int id, const std::string &name, const IO::Astrodynamics::Math::Vector3D &orientation,
                   const IO::Astrodynamics::Math::Vector3D &boresight, const IO::Astrodynamics::Math::Vector3D &fovRefVector, double fovAngle);

        /**
         * @brief Construct a new rectangular or elliptical instrument object
         *
         * @param spacecraft
         * @param id
         * @param name
         * @param orientation
         * @param fovShape
         * @param boresight
         * @param fovRefVector
         * @param fovAngle
         * @param crossAngle
         */
        Instrument(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft, int id, const std::string &name, const IO::Astrodynamics::Math::Vector3D &orientation,
                   IO::Astrodynamics::Instruments::FOVShapeEnum fovShape, const IO::Astrodynamics::Math::Vector3D &boresight, const IO::Astrodynamics::Math::Vector3D &fovRefVector, double fovAngle,
                   double crossAngle);

    public:
        ~Instrument() = default;

        /**
         * @brief Get the Files Path object
         *
         * @return std::string
         */
        [[nodiscard]] std::string GetFilesPath() const;

        /**
         * @brief Get the Name object
         *
         * @return std::string
         */
        [[nodiscard]] std::string GetName() const;

        /**
         * @brief Get the Id object
         *
         * @return int
         */
        [[nodiscard]] int GetId() const;

        /**
         * @brief Get the Spacecraft object
         *
         * @return const IO::Astrodynamics::Body::Spacecraft&
         */
        [[nodiscard]] const IO::Astrodynamics::Body::Spacecraft::Spacecraft &GetSpacecraft() const;

        /**
         * @brief Get the Frame object
         *
         * @return const std::unique_ptr<IO::Astrodynamics::Frames::InstrumentFrame>&
         */
        [[nodiscard]] const std::unique_ptr<IO::Astrodynamics::Frames::InstrumentFrameFile> &GetFrame() const;

        /**
         * @brief Get the Boresight vector
         *
         * @return IO::Astrodynamics::Math::Vector3D
         */
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetBoresight() const;

        /**
         * Get the boresight vector at specified epoch in a given frame
         * @param frame
         * @param epoch
         * @return IO::Astrodynamics::Math::Vector3D
         */
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetBoresight(const IO::Astrodynamics::Frames::Frames &frame, const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Get the FOV shape
         *
         * @return IO::Astrodynamics::Instruments::FOVShapeEnum
         */
        [[nodiscard]] IO::Astrodynamics::Instruments::FOVShapeEnum GetFOVShape() const;

        /**
         * @brief Get the FOV boundaries
         *
         * @return std::vector<IO::Astrodynamics::Math::Vector3D>
         */
        [[nodiscard]] std::vector<IO::Astrodynamics::Math::Vector3D> GetFOVBoundaries() const;

        /**
         * @brief Find window where target body is in field of view
         *
         * @param searchWindow
         * @param targetBody
         * @param stepSize
         * @return std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
         */
        [[nodiscard]] std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsWhereInFieldOfView(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, const IO::Astrodynamics::Body::Body &targetBody,
                                      const IO::Astrodynamics::AberrationsEnum &aberration, const IO::Astrodynamics::Time::TimeSpan &stepSize) const;


        /**
         * Find windows where a site is in field of vien
         * @param searchWindow
         * @param site
         * @param aberration
         * @param stepSize
         * @return
         */
        [[nodiscard]] std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
        FindWindowsWhereInFieldOfView(
                const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, const IO::Astrodynamics::Sites::Site &site,
                const IO::Astrodynamics::AberrationsEnum &aberration,
                const IO::Astrodynamics::Time::TimeSpan &stepSize
        ) const;

        /**
         * Compute boresight in Spacecraft frame
         * @return
         */
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetBoresightInSpacecraftFrame() const;

        friend class IO::Astrodynamics::Body::Spacecraft::Spacecraft;
    };
}

#endif // !INSTRUMENT_H
