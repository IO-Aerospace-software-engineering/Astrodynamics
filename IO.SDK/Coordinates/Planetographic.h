/**
 * @file Planetographic.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef PLANETOGRAPHIC_H
#define PLANETOGRAPHIC_H
namespace IO::SDK::Coordinates
{
	/**
	 * @brief Planetographic coordinate
	 * 
	 */
	class Planetographic
	{
	private:
		const double _altitude, _longitude, _latitude;

	public:
		/**
		 * @brief Construct a new Planetographic object
		 * 
		 * @param longitude 
		 * @param latitude 
		 * @param altitude 
		 */
		Planetographic(const double longitude, const double latitude, const double altitude) :_altitude{ altitude }, _longitude{ longitude }, _latitude{ latitude }
		{

		}
		/**
		 * @brief Get the Altitude
		 * 
		 * @return double 
		 */
		double GetAltitude() const { return this->_altitude; }

		/**
		 * @brief Get the Longitude
		 * 
		 * @return double 
		 */
		double GetLongitude() const { return this->_longitude; }

		/**
		 * @brief Get the Latitude
		 * 
		 * @return double 
		 */
		double GetLatitude() const { return this->_latitude; }
	};
}
#endif // !PLANETOGRAPHIC_H


