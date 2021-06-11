/**
 * @file HorizontalCoordinates.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-05-19
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef HORIZONTAL_COORDINATES_H
#define HORIZONTAL_COORDINATES_H
namespace IO::SDK::Coordinates
{
    /**
	 * @brief Horizontal coordinates class
	 * 
	 */
    class HorizontalCoordinates
    {
    private:
        double m_altitude{}, m_azimuth{}, m_elevation{};

    public:
        /**
		 * @brief Construct a new Horizontal Coordinates object
		 * 
		 * @param azimuth 
		 * @param elevation 
		 * @param altitude 
		 */
        HorizontalCoordinates(double azimuth, double elevation, double altitude) : m_altitude{altitude}, m_azimuth{azimuth}, m_elevation{elevation}
        {
        }
        /**
		 * @brief Get the Altitude
		 * 
		 * @return double 
		 */
        double GetAltitude() { return this->m_altitude; }

        /**
		 * @brief Get the Azimuth
		 * 
		 * @return double 
		 */
        double GetAzimuth() { return this->m_azimuth; }

        /**
		 * @brief Get the Elevation
		 * 
		 * @return double 
		 */
        double GetElevation() { return this->m_elevation; }
    };
}
#endif // !HORIZONTAL_COORDINATES_H
