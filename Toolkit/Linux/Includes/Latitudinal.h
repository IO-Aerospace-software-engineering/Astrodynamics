/**
 * @file Latitudinal.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef LATITUDINAL_H
#define LATITUDINAL_H
namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Latitudinal coordinates
	/// </summary>
	class Latitudinal
	{
	private:
		const double _radius, _longitude, _latitude;

	public:
		/// <summary>
		/// Instanciate latitudinal coordinates
		/// </summary>
		/// <param name="radius">radius in meter</param>
		/// <param name="longitude">longitude in radian</param>
		/// <param name="latitude">latitude in radian</param>
		Latitudinal(const double longitude, const double latitude, const double radius) :_radius{ radius }, _longitude{ longitude }, _latitude{ latitude }
		{

		}

		/// <summary>
		/// Get the radius
		/// </summary>
		/// <returns></returns>
		double GetRadius() const { return this->_radius; }

		/// <summary>
		/// Get the longitude in radians
		/// </summary>
		/// <returns></returns>
		double GetLongitude() const { return this->_longitude; }

		/// <summary>
		/// Get the latitude in radians
		/// </summary>
		/// <returns></returns>
		double GetLatitude() const { return this->_latitude; }
	};
}
#endif // !LATITUDINAL_H


