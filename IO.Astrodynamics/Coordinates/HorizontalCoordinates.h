/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef HORIZONTAL_COORDINATES_H
#define HORIZONTAL_COORDINATES_H
namespace IO::Astrodynamics::Coordinates {
    /**
     * @brief Horizontal coordinates class
     *
     */
    class HorizontalCoordinates {
    private:
        const double m_altitude, m_azimuth, m_elevation;

    public:
        /**
         * @brief Construct a new Horizontal Coordinates object
         *
         * @param azimuth
         * @param elevation
         * @param altitude
         */
        HorizontalCoordinates(const double azimuth, const double elevation, const double altitude) : m_altitude{altitude}, m_azimuth{azimuth}, m_elevation{elevation} {
        }

        HorizontalCoordinates &operator=(const HorizontalCoordinates &rhs) {
            if (this != &rhs) {
                const_cast<double &>(m_altitude) = rhs.m_altitude;
                const_cast<double &>(m_azimuth) = rhs.m_azimuth;
                const_cast<double &>(m_elevation) = rhs.m_elevation;
            }

            return *this;
        }

        HorizontalCoordinates(const HorizontalCoordinates &horizontalCoordinates) = default;

        /**
         * @brief Get the Altitude
         *
         * @return double
         */
        [[nodiscard]] inline double GetAltitude() const { return this->m_altitude; }

        /**
         * @brief Get the Azimuth
         *
         * @return double
         */
        [[nodiscard]] inline double GetAzimuth() const { return this->m_azimuth; }

        /**
         * @brief Get the Elevation
         *
         * @return double
         */
        [[nodiscard]] inline double GetElevation() const { return this->m_elevation; }
    };
}
#endif // !HORIZONTAL_COORDINATES_H
