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
		double _radius{}, _longitude{}, _z{};

	public:
		/// <summary>
		/// Instanciate cylindrical coordinates
		/// </summary>
		/// <param name="radius">radius</param>
		/// <param name="longitude">longitude in radians</param>
		/// <param name="z">z</param>
		Cylindrical(double radius, double longitude, double z) :_radius{ radius }, _longitude{ longitude }, _z{ z }
		{

		}
		
		/// <summary>
		/// Get the radius
		/// </summary>
		/// <returns></returns>
		double GetRadius() { return this->_radius; }
		
		
		double GetLongitude() { return this->_longitude; }

		
		double GetZ() { return this->_z; }
	};
}
#endif // !CYLINDRICAL_H


