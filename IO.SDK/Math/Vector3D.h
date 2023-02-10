/**
 * @file Vector3D.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-04-13
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef VECTOR3D_H
#define VECTOR3D_H

namespace IO::SDK::Math
{
	class Quaternion;

	class Vector3D
	{
	private:
		double const m_x{}, m_y{}, m_z{};

	public:
		static const Vector3D VectorX;
		static const Vector3D VectorY;
		static const Vector3D VectorZ;
		static const Vector3D Zero;
		Vector3D(){};
		/// <summary>
		/// Instantiate a 3D vector
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		Vector3D(double x, double y, double z) : m_x{x}, m_y{y}, m_z{z} {};

		Vector3D(const Vector3D &vector) : m_x{vector.m_x}, m_y{vector.m_y}, m_z{vector.m_z} {};

		Vector3D &operator=(const Vector3D &vector);

		~Vector3D() = default;

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

		/// <summary>
		/// Get the vector magnitude
		/// </summary>
		/// <returns></returns>
		double Magnitude() const;

		Vector3D const operator+(const Vector3D &vector) const;
		Vector3D const operator-(const Vector3D &vector) const;
		Vector3D const operator*(const double value) const;
		Vector3D const operator/(const double value) const;
		

		/// <summary>
		/// Get the cross product from another vector
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		Vector3D CrossProduct(const Vector3D &vector) const;

		/// <summary>
		/// Get the dot product
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		double DotProduct(const Vector3D &vector) const;

		/// <summary>
		/// Get this normalized vector
		/// </summary>
		/// <returns></returns>
		Vector3D Normalize() const;

		/// <summary>
		/// Get angle from another vector
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		double GetAngle(const Vector3D &vector) const;

		/**
		 * @brief Equal
		 * 
		 * @param vector 
		 * @return true 
		 * @return false 
		 */
		bool operator==(const Vector3D &vector) const;

		/**
		 * @brief Rotate vector by quaternion
		 * 
		 * @param quaternion 
		 * @return Vector3D 
		 */
		Vector3D Rotate(const IO::SDK::Math::Quaternion &quaternion) const;

		/**
		 * @brief Return the quaternion to rotate vector to another
		 * 
		 * @param vector 
		 * @return Quaternion 
		 */
		Quaternion To(const Vector3D &vector) const;

		/**
		 * @brief Reverse vector
		 * 
		 * @return Vector3D 
		 */
		Vector3D Reverse() const;
	};
}
#endif // !VECTOR3D_H
