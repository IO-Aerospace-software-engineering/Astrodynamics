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
		const double m_x, m_y, m_z;

	public:
		/// <summary>
		/// Instanciate rectangular coordinates
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		Rectangular(const double x, const double y,const double z) :m_x{ x }, m_y{ y }, m_z{ z }
		{

		}

		/// <summary>
		/// Get X
		/// </summary>
		/// <returns></returns>
		double GetX() const { return this->m_x; }

		/// <summary>
		/// Get Y
		/// </summary>
		/// <returns></returns>
		double GetY() const { return this->m_y; }

		/// <summary>
		/// Get Z
		/// </summary>
		/// <returns></returns>
		double GetZ() const { return this->m_z; }
	};
}
#endif // !RECTANGULAR_H


