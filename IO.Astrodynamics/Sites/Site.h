/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef SITE_H
#define SITE_H


#include <Geodetic.h>
#include <Illumination.h>
#include <SiteFrameFile.h>
#include <HorizontalCoordinates.h>
#include <IlluminationAngle.h>
#include <EphemerisKernel.h>

namespace IO::Astrodynamics::Kernels
{
    class EphemerisKernel;
}

namespace IO::Astrodynamics::Sites
{
    /**
     * @brief Site class
     * 
     */
    class Site
    {
    protected:
        const int m_id;
        const std::string m_name;
        const IO::Astrodynamics::Coordinates::Geodetic m_coordinates;
        const std::string m_filesPath;
        const std::unique_ptr<IO::Astrodynamics::Kernels::EphemerisKernel> m_ephemerisKernel;

        const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> m_body;
        const std::unique_ptr<IO::Astrodynamics::Frames::SiteFrameFile> m_frame;
        /**
         * Write stateVectors into ephemeris file
         * @param states
         */
        void WriteEphemeris(const std::vector<OrbitalParameters::StateVector> &states) const;

    public:
        /**
         * @brief Construct a new Site object
         * 
         * @param name 
         * @param coordinates 
         */
        Site(int id, std::string name, const IO::Astrodynamics::Coordinates::Geodetic &coordinates,
             std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> body, std::string directoryPath);

        virtual ~Site() = default;

        /**
         * @brief Get the Id
         * 
         * @return int 
         */
        [[nodiscard]] inline int GetId() const
        { return m_id; }

        /**
         * @brief Get the Name
         * 
         * @return std::string 
         */
        [[nodiscard]] inline std::string GetName() const
        { return m_name; }

        /**
         * @brief Get the Coordinates
         * 
         * @return IO::Astrodynamics::Coordinates::Geodetic
         */
        [[nodiscard]] inline IO::Astrodynamics::Coordinates::Geodetic GetCoordinates() const
        { return m_coordinates; }

        /**
         * @brief Get the Body
         * 
         * @return std::shared_ptr<IO::Astrodynamics::Body::CelestialBody>
         */
        [[nodiscard]] inline std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> GetBody() const
        { return m_body; }

        /**
         * @brief Get Right ascension, declination and range
         * 
         * @param body Target body
         * @param epoch 
         * @return IO::Astrodynamics::Coordinates::Equatorial
         */
        [[nodiscard]] IO::Astrodynamics::Coordinates::Equatorial GetRADec(const IO::Astrodynamics::Body::Body &body, IO::Astrodynamics::AberrationsEnum aberrationCorrection, const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Get rectangular position
         * 
         * @return IO::Astrodynamics::Math::Vector3D
         */
        [[nodiscard]] IO::Astrodynamics::OrbitalParameters::StateVector GetStateVector(const IO::Astrodynamics::Frames::Frames& frame, const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Get the Illumination
         * 
         * @param aberrationCorrection 
         * @param epoch 
         * @return IO::Astrodynamics::Illumination::Illumination
         */
        [[nodiscard]] IO::Astrodynamics::Illumination::Illumination GetIllumination(IO::Astrodynamics::AberrationsEnum aberrationCorrection, const IO::Astrodynamics::Time::TDB &epoch) const;

        /**
         * @brief Know if it's day
         * 
         * @param epoch 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsDay(const IO::Astrodynamics::Time::TDB &epoch, double twilight) const;

        /**
         * @brief Know if it's night
         * 
         * @param epoch 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsNight(const IO::Astrodynamics::Time::TDB &epoch, double twilight) const;

        /**
         * @brief Find day windows
         * 
         * @param searchWindow 
         * @param twilight 
         * @return std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
         */
        [[nodiscard]] std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>> FindDayWindows(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow, double twilight) const;

        /**
         * @brief Find night windows
         * 
         * @param searchWindow 
         * @param twilight 
         * @return std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
         */
        [[nodiscard]] std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>> FindNightWindows(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow, double twilight) const;

        /**
         * @brief Find windows where illumination constraint is satisfied
         * 
         * @param searchWindow 
         * @param targetBody 
         * @param observerBody 
         * @param illuminationAngle
         * @param constraint 
         * @param value 
         * @return std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
         */
        [[nodiscard]] std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
        FindWindowsOnIlluminationConstraint(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow, const IO::Astrodynamics::Body::Body &observerBody,
                                            const IO::Astrodynamics::IlluminationAngle &illuminationAngle, const IO::Astrodynamics::Constraints::RelationalOperator &constraint, double value) const;

        /**
         * @brief Get the Horizontal Coordinates to the target body
         * 
         * @param body 
         * @param epoch 
         * @return IO::Astrodynamics::Coordinates::HorizontalCoordinates
         */
        [[nodiscard]] IO::Astrodynamics::Coordinates::HorizontalCoordinates
        GetHorizontalCoordinates(const IO::Astrodynamics::Body::Body &body, IO::Astrodynamics::AberrationsEnum aberrationCorrection, const IO::Astrodynamics::Time::TDB &epoch) const;


        /**
         * @brief Get the State Vector to target body
         * 
         * @param body 
         * @param aberrationCorrection 
         * @param epoch 
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        [[nodiscard]] IO::Astrodynamics::OrbitalParameters::StateVector
        GetStateVector(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::Frames::Frames& frame, IO::Astrodynamics::AberrationsEnum aberrationCorrection,
                       const IO::Astrodynamics::Time::TDB &epoch) const;


        [[nodiscard]] std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC>>
        FindBodyVisibilityWindows(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow,
                                  IO::Astrodynamics::AberrationsEnum aberrationCorrection) const;

        /**
         * Get the site frame file
         * @return
         */
        [[nodiscard]] inline const std::unique_ptr<IO::Astrodynamics::Frames::SiteFrameFile> &GetFrame() const
        { return m_frame; }

        /**
         * Get the file path to this site
         * @return
         */
        [[nodiscard]] inline std::string GetFilesPath() const
        { return m_filesPath; }



        /**
         * Build and write ephemeris for a given period
         * @param searchWindow
         */
        void BuildAndWriteEphemeris(const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &searchWindow) const;

        /**
         * Read ephemeris from ephemeris file
         * @param frame
         * @param aberration
         * @param epoch
         * @param observer
         * @return
         */
        [[nodiscard]] OrbitalParameters::StateVector
        ReadEphemeris(const Frames::Frames &frame, AberrationsEnum aberration, const Time::TDB &epoch, const Body::CelestialBody &observer) const;

        /**
         * Get Time span covered by the ephemeris file
         * @return
         */
        [[nodiscard]] Time::Window<IO::Astrodynamics::Time::TDB> GetEphemerisCoverageWindow() const;

        /**
         * Write a comment into ephemeris file
         * @param comment
         */
        void WriteEphemerisKernelComment(const std::string &comment) const;

        /**
         * Read comment into ephemeris file
         * @return
         */
        [[nodiscard]] std::string ReadEphemerisKernelComment() const;
    };
}

#endif