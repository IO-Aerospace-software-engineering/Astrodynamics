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
	/// <summary>
	/// Planetographic coordinates
	/// </summary>
	class Planetographic
	{
	private:
		const double _altitude, _longitude, _latitude;

	public:
		/// <summary>
		/// Instanciate planetographic coordinates
		/// </summary>
		/// <param name="altitude">altitude in meter</param>
		/// <param name="longitude">longitude in radian</param>
		/// <param name="latitude">latitude in radian</param>
		Planetographic(const double longitude, const double latitude, const double altitude) :_altitude{ altitude }, _longitude{ longitude }, _latitude{ latitude }
		{

		}
		/// <summary>
		/// Get the altitude
		/// </summary>
		/// <returns></returns>
		double GetAltitude() const { return this->_altitude; }

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
#endif // !PLANETOGRAPHIC_H


