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
		double _radius{}, _longitude{}, _latitude{};

	public:
		/// <summary>
		/// Instanciate latitudinal coordinates
		/// </summary>
		/// <param name="radius">radius in meter</param>
		/// <param name="longitude">longitude in radian</param>
		/// <param name="latitude">latitude in radian</param>
		Latitudinal(double longitude, double latitude, double radius) :_radius{ radius }, _longitude{ longitude }, _latitude{ latitude }
		{

		}

		/// <summary>
		/// Get the radius
		/// </summary>
		/// <returns></returns>
		double GetRadius() { return this->_radius; }

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
#endif // !LATITUDINAL_H


