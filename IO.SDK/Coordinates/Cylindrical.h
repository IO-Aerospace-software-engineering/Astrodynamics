#ifndef CYLINDRICAL_H
#define CYLINDRICAL_H
namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Cylindrical coordinates
	/// </summary>
	class Cylindrical
	{
	private:
		const double _radius, _longitude, _z;

	public:
		/// <summary>
		/// Instanciate cylindrical coordinates
		/// </summary>
		/// <param name="radius">radius</param>
		/// <param name="longitude">longitude in radians</param>
		/// <param name="z">z</param>
		Cylindrical(const double radius, const double longitude, const double z) : _radius{radius}, _longitude{longitude}, _z{z}
		{
		}

		/// <summary>
		/// Get the radius
		/// </summary>
		/// <returns></returns>
		double GetRadius() const { return this->_radius; }

		double GetLongitude() const { return this->_longitude; }

		double GetZ() const { return this->_z; }
	};
}
#endif // !CYLINDRICAL_H
