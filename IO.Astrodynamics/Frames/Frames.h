/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef FRAMES_H
#define FRAMES_H

#include <string>
#include <Matrix.h>
#include <TDB.h>
#include <Vector3D.h>

namespace IO::Astrodynamics::Frames
{
    /**
     * @brief Frames base class
     * 
     */
    class Frames
    {
    protected:
        std::string m_name;

    public:
        /**
         * @brief Construct a new Frames object
         * 
         * @param strView 
         */
        explicit Frames(std::string strView);

        /**
         * @brief Get frame name
         * 
         * @return const char* 
         */
        [[nodiscard]] const char* ToCharArray() const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator==(const IO::Astrodynamics::Frames::Frames& frame) const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator!=(const IO::Astrodynamics::Frames::Frames& frame) const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator==(IO::Astrodynamics::Frames::Frames& frame) const;

        /**
         * @brief Equality comparer
         * 
         * @param frame 
         * @return true 
         * @return false 
         */
        bool operator!=(IO::Astrodynamics::Frames::Frames& frame) const;

        /**
         * @brief Get the Name
         * 
         * @return std::string 
         */
        [[nodiscard]] std::string GetName() const;

        /**
         * @brief Get the 6X6 matrix to transform frame to another
         * 
         * @param frame 
         * @param epoch 
         * @return IO::Astrodynamics::Math::Matrix
         */
        [[nodiscard]] IO::Astrodynamics::Math::Matrix ToFrame6x6(const Frames& frame,
                                                                 const IO::Astrodynamics::Time::TDB& epoch) const;

        /**
         * @brief Get the 3x3 matrix to transform frame to another
         * 
         * @param frame 
         * @param epoch 
         * @return IO::Astrodynamics::Math::Matrix
         */
        [[nodiscard]] IO::Astrodynamics::Math::Matrix ToFrame3x3(const Frames& frame,
                                                                 const IO::Astrodynamics::Time::TDB& epoch) const;

        /**
         * @brief Transform vector from frame to another
         * 
         * @param from 
         * @param to 
         * @param vector 
         * @return IO::Astrodynamics::Math::Vector3D
         */
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D TransformVector(
            const Frames& to, const IO::Astrodynamics::Math::Vector3D& vector,
            const IO::Astrodynamics::Time::TDB& epoch) const;


        /**
         * @brief Convert from TEME (True Equator Mean Equinox) to ITRF (International Terrestrial Reference Frame).
         *
         * @param epoch The epoch time in UTC.
         * @return IO::Astrodynamics::Math::Matrix The transformation matrix.
         */
        [[nodiscard]] static IO::Astrodynamics::Math::Matrix FromTEMEToITRF(const IO::Astrodynamics::Time::UTC& epoch);

        /**
         * @brief Convert from ITRF (International Terrestrial Reference Frame) to TEME (True Equator Mean Equinox).
         *
         * @param epoch The epoch time in UTC.
         * @return IO::Astrodynamics::Math::Matrix The transformation matrix.
         */
        static IO::Astrodynamics::Math::Matrix FromITRFToTEME(const IO::Astrodynamics::Time::UTC& epoch);

        /**
         * @brief Convert from TEME (True Equator Mean Equinox) to GCRS (Geocentric Celestial Reference System).
         *
         * @param epoch The epoch time in UTC.
         * @return IO::Astrodynamics::Math::Matrix The transformation matrix.
         */
        [[nodiscard]] static IO::Astrodynamics::Math::Matrix FromTEMEToGCRS(const IO::Astrodynamics::Time::UTC& epoch);

        /**
         * @brief Calculate the polar motion matrix.
         *
         * @param epoch The epoch time in UTC.
         * @return IO::Astrodynamics::Math::Matrix The polar motion matrix.
         */
        [[nodiscard]] static IO::Astrodynamics::Math::Matrix PolarMotion(const IO::Astrodynamics::Time::UTC& epoch);
    };
}
#endif
