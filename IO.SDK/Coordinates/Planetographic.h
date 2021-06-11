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
		double _altitude{}, _longitude{}, _latitude{};

	public:
		/// <summary>
		/// Instanciate planetographic coordinates
		/// </summary>
		/// <param name="altitude">altitude in meter</param>
		/// <param name="longitude">longitude in radian</param>
		/// <param name="latitude">latitude in radian</param>
		Planetographic(double longitude, double latitude, double altitude) :_altitude{ altitude }, _longitude{ longitude }, _latitude{ latitude }
		{

		}
		/// <summary>
		/// Get the altitude
		/// </summary>
		/// <returns></returns>
		double GetAltitude() { return this->_altitude; }

		/// <summary>
		/// Get the longitude in radians
		/// </summary>
		/// <returns></returns>
		double GetLongitude() { return this->_longitude; }

		/// <summary>
		/// Get the latitude in radians
		/// </summary>
		/// <returns></returns>
		double GetLatitude() { return this->_latitude; }
	};
}
#endif // !PLANETOGRAPHIC_H


