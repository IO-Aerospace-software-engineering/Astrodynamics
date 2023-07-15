/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef GEODETIC_H
#define GEODETIC_H
namespace IO::Astrodynamics::Coordinates
{
	/**
	 * @brief Geodetic coordinate
	 * 
	 */
	class Planetodetic
	{
	private:
		const double _altitude, _longitude, _latitude;

	public:
		
		/**
		 * @brief Construct a new Geodetic object
		 * 
		 * @param longitude 
		 * @param latitude 
		 * @param altitude 
		 */
		Planetodetic(const double longitude, const double latitude, const double altitude) : _altitude{altitude }, _longitude{longitude }, _latitude{latitude }
		{

		}

		/**
		 * @brief Get the Altitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetAltitude() const { return this->_altitude; }

		/**
		 * @brief Get the Longitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetLongitude() const { return this->_longitude; }
		
		/**
		 * @brief Get the Latitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetLatitude() const { return this->_latitude; }
	};
}
#endif // !GEODETIC_H


