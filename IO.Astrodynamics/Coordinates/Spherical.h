/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SPHERICAL_H
#define SPHERICAL_H
namespace IO::SDK::Coordinates
{
	/**
	 * @brief Get spherical coordinate
	 * 
	 */
	class Spherical
	{
	private:
		const double _radius, _longitude, _colatitude;

	public:
		/**
		 * @brief Construct a new Spherical object
		 * 
		 * @param longitude 
		 * @param colatitude 
		 * @param radius 
		 */
		Spherical(const double longitude, const double colatitude, const double radius) : _radius{radius}, _longitude{longitude}, _colatitude{colatitude}
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
		 * @brief Get the Colatitude
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetColatitude() const { return this->_colatitude; }
	};
}
#endif // !SPHERICAL_H
