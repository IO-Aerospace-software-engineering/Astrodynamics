#ifndef RECTANGULAR_H
#define RECTANGULAR_H
namespace IO::SDK::Coordinates
{
	/// <summary>
	/// Rectangular coordinates
	/// </summary>
	class Rectangular
	{
	private:
		double _x{}, _y{}, _z{};

	public:
		/// <summary>
		/// Instanciate rectangular coordinates
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		Rectangular(double x, double y, double z) :_x{ x }, _y{ y }, _z{ z }
		{

		}

		/// <summary>
		/// Get X
		/// </summary>
		/// <returns></returns>
		double GetX() { return this->_x; }

		/// <summary>
		/// Get Y
		/// </summary>
		/// <returns></returns>
		double GetY() { return this->_y; }

		/// <summary>
		/// Get Z
		/// </summary>
		/// <returns></returns>
		double GetZ() { return this->_z; }
	};
}
#endif // !RECTANGULAR_H


