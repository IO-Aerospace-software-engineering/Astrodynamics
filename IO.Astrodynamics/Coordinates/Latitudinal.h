/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef LATITUDINAL_H
#define LATITUDINAL_H
namespace IO::Astrodynamics::Coordinates
{
	/**
	 * @brief 
	 * 
	 */
	class Latitudinal
	{
	private:
		const double _radius, _longitude, _latitude;

	public:
		/**
		 * @brief Construct a new Latitudinal object
		 * 
		 * @param longitude 
		 * @param latitude 
		 * @param radius 
		 */
		Latitudinal(const double longitude, const double latitude, const double radius) :_radius{ radius }, _longitude{ longitude }, _latitude{ latitude }
		{

		}

		/**
		 * @brief Get the Radius
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetRadius() const { return this->_radius; }

		/**
		 * @brief Get the Longitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetLongitude() const { return this->_longitude; }

		/**
		 * @brief Get the Latitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetLatitude() const { return this->_latitude; }
	};
}
#endif // !LATITUDINAL_H


