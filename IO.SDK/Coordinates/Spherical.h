#ifndef SPHERICAL_H
#define SPHERICAL_H
namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Spherical coordinates
	/// </summary>
	class Spherical
	{
	private:
		const double _radius, _longitude, _colatitude;

	public:
		/// <summary>
		/// Instanciate spherical coordinates
		/// </summary>
		/// <param name="radius">radius in meter</param>
		/// <param name="longitude">longitude in radian</param>
		/// <param name="colatitude">colatitude in radian</param>
		Spherical(const double longitude, const double colatitude, const double radius) : _radius{radius}, _longitude{longitude}, _colatitude{colatitude}
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
		/// Get the colatitude in radians
		/// </summary>
		/// <returns></returns>
		double GetColatitude() const { return this->_colatitude; }
	};
}
#endif // !SPHERICAL_H
