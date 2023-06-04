/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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
		Planetographic(double longitude, double latitude, double altitude) :_altitude{ altitude }, _longitude{ longitude }, _latitude{ latitude }
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
#endif // !PLANETOGRAPHIC_H


