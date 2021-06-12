#ifndef GEODETIC_H
#define GEODETIC_H
namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Geodetic coordinates
	/// </summary>
	class Geodetic
	{
	private:
		const double _altitude, _longitude, _latitude;

	public:
		/// <summary>
		/// Instanciate geodetic coordinates
		/// </summary>
		/// <param name="altitude">altitude in meter</param>
		/// <param name="longitude">longitude in radian</param>
		/// <param name="latitude">latitude in radian</param>
		Geodetic(const double longitude, const double latitude, const double altitude) :_altitude{ altitude }, _longitude{ longitude }, _latitude{ latitude }
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
#endif // !GEODETIC_H


