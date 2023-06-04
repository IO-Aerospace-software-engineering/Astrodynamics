/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef CYLINDRICAL_H
#define CYLINDRICAL_H
namespace IO::SDK::Coordinates
{
	/**
	 * @brief 
	 * 
	 */
	class Cylindrical
	{
	private:
		const double _radius, _longitude, _z;

	public:
		/**
		 * @brief Construct a new Cylindrical object
		 * 
		 * @param radius 
		 * @param longitude 
		 * @param z 
		 */
		Cylindrical(const double radius, const double longitude, const double z) : _radius{radius}, _longitude{longitude}, _z{z}
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
		 * @brief 
		 * 
		 * @return double 
		 */
		[[nodiscard]] double GetZ() const { return this->_z; }
	};
}
#endif // !CYLINDRICAL_H
