/**
 * @file Site.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-05-06
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef SITE_H
#define SITE_H


#include <Geodetic.h>
#include <Illumination.h>
#include <SiteFrameFile.h>
#include <HorizontalCoordinates.h>
#include <IlluminationAngle.h>
#include <EphemerisKernel.h>

namespace IO::SDK::Kernels
{
    class EphemerisKernel;
}

namespace IO::SDK::Sites
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
        const IO::SDK::Coordinates::Geodetic m_coordinates;
        const std::string m_filePath;
        const std::unique_ptr<IO::SDK::Kernels::EphemerisKernel> m_ephemerisKernel;

        const std::shared_ptr<IO::SDK::Body::CelestialBody> m_body;
        const std::unique_ptr<IO::SDK::Frames::SiteFrameFile> m_frame;
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
        Site(int id, const std::string &name, const IO::SDK::Coordinates::Geodetic &coordinates,
             std::shared_ptr<IO::SDK::Body::CelestialBody> &body);

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
         * @return IO::SDK::Coordinates::Geodetic 
         */
        [[nodiscard]] inline IO::SDK::Coordinates::Geodetic GetCoordinates() const
        { return m_coordinates; }

        /**
         * @brief Get the Body
         * 
         * @return std::shared_ptr<IO::SDK::Body::CelestialBody> 
         */
        [[nodiscard]] inline std::shared_ptr<IO::SDK::Body::CelestialBody> GetBody() const
        { return m_body; }

        /**
         * @brief Get Right ascension, declination and range
         * 
         * @param body Target body
         * @param epoch 
         * @return IO::SDK::Coordinates::RADec 
         */
        [[nodiscard]] IO::SDK::Coordinates::RADec GetRADec(const IO::SDK::Body::Body &body, IO::SDK::AberrationsEnum aberrationCorrection, const IO::SDK::Time::TDB &epoch) const;

        /**
         * @brief Get rectangular position
         * 
         * @return IO::SDK::Math::Vector3D 
         */
        [[nodiscard]] IO::SDK::OrbitalParameters::StateVector GetStateVector(const IO::SDK::Frames::Frames& frame, const IO::SDK::Time::TDB &epoch) const;

        /**
         * @brief Get the Illumination
         * 
         * @param aberrationCorrection 
         * @param epoch 
         * @return IO::SDK::Illumination::Illumination 
         */
        [[nodiscard]] IO::SDK::Illumination::Illumination GetIllumination(IO::SDK::AberrationsEnum aberrationCorrection, const IO::SDK::Time::TDB &epoch) const;

        /**
         * @brief Know if it's day
         * 
         * @param epoch 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsDay(const IO::SDK::Time::TDB &epoch, double twilight) const;

        /**
         * @brief Know if it's night
         * 
         * @param epoch 
         * @return true 
         * @return false 
         */
        [[nodiscard]] bool IsNight(const IO::SDK::Time::TDB &epoch, double twilight) const;

        /**
         * @brief Find day windows
         * 
         * @param searchWindow 
         * @param twilight 
         * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> 
         */
        [[nodiscard]] std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> FindDayWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow, double twilight) const;

        /**
         * @brief Find night windows
         * 
         * @param searchWindow 
         * @param twilight 
         * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> 
         */
        [[nodiscard]] std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> FindNightWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow, double twilight) const;

        /**
         * @brief Find windows where illumination constraint is satisfied
         * 
         * @param searchWindow 
         * @param targetBody 
         * @param observerBody 
         * @param illuminationAngle
         * @param constraint 
         * @param value 
         * @return std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> 
         */
        [[nodiscard]] std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
        FindWindowsOnIlluminationConstraint(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow, const IO::SDK::Body::Body &observerBody,
                                            const IO::SDK::IlluminationAngle &illuminationAngle, const IO::SDK::Constraints::Constraint &constraint, double value) const;

        /**
         * @brief Get the Horizontal Coordinates to the target body
         * 
         * @param body 
         * @param epoch 
         * @return IO::SDK::Coordinates::HorizontalCoordinates 
         */
        [[nodiscard]] IO::SDK::Coordinates::HorizontalCoordinates
        GetHorizontalCoordinates(const IO::SDK::Body::Body &body, IO::SDK::AberrationsEnum aberrationCorrection, const IO::SDK::Time::TDB &epoch) const;


        /**
         * @brief Get the State Vector to target body
         * 
         * @param body 
         * @param aberrationCorrection 
         * @param epoch 
         * @return IO::SDK::OrbitalParameters::StateVector 
         */
        [[nodiscard]] IO::SDK::OrbitalParameters::StateVector
        GetStateVector(const IO::SDK::Body::Body &body, const IO::SDK::Frames::Frames& frame, IO::SDK::AberrationsEnum aberrationCorrection,
                       const IO::SDK::Time::TDB &epoch) const;


        [[nodiscard]] std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>
        FindBodyVisibilityWindows(const IO::SDK::Body::Body &body, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                  IO::SDK::AberrationsEnum aberrationCorrection) const;

        /**
         * Get the site frame file
         * @return
         */
        [[nodiscard]] inline const std::unique_ptr<IO::SDK::Frames::SiteFrameFile> &GetFrame() const
        { return m_frame; }

        /**
         * Get the file path to this site
         * @return
         */
        [[nodiscard]] inline std::string GetFilesPath() const
        { return m_filePath; }



        /**
         * Build and write ephemeris for a given period
         * @param searchWindow
         */
        void BuildAndWriteEphemeris(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow) const;

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
        [[nodiscard]] Time::Window<IO::SDK::Time::TDB> GetEphemerisCoverageWindow() const;

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