/**
 * @file Rectangular.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef RECTANGULAR_H
#define RECTANGULAR_H
namespace IO::SDK::Coordinates
{
	/**
	 * @brief Rectangular coordinates
	 * 
	 */
	class Rectangular
	{
	private:
		const double m_x, m_y, m_z;

	public:
		/**
		 * @brief Construct a new Rectangular object
		 * 
		 * @param x 
		 * @param y 
		 * @param z 
		 */
		Rectangular(double x, double y,double z) :m_x{ x }, m_y{ y }, m_z{ z }
		{

		}

		/**
		 * @brief Get X
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetX() const { return this->m_x; }

		/**
		 * @brief Get Y
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetY() const { return this->m_y; }

		/**
		 * @brief Get Z
		 * 
		 * @return double 
		 */
		[[nodiscard]] inline double GetZ() const { return this->m_z; }
	};
}
#endif // !RECTANGULAR_H


