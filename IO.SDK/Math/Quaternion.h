/**
 * @file Quaternion.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-04-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef QUATERNION_H
#define QUATERNION_H

#include <Matrix.h>
#include <Vector3D.h>

namespace IO::SDK::Math
{
	class Quaternion
	{
	private:
		const double m_q0{}, m_q1{}, m_q2{}, m_q3{};

	public:
		/**
		 * @brief Construct a new Quaternion object
		 * 
		 */
		Quaternion();

		/**
		 * @brief Construct a new Quaternion object
		 * 
		 * @param q0 
		 * @param q1 
		 * @param q2 
		 * @param q3 
		 */
		Quaternion(const double q0, const double q1, const double q2, const double q3);

		/**
		 * @brief Construct a new Quaternion object
		 * 
		 * @param axis 
		 * @param angle 
		 */
		Quaternion(const IO::SDK::Math::Vector3D &axis, const double angle);

		/**
		 * @brief Construct a new Quaternion object
		 * 
		 * @param mtx 
		 */
		Quaternion(const IO::SDK::Math::Matrix &mtx);

		/**
		 * @brief Construct a new Quaternion object
		 * 
		 * @param quaternion 
		 */
		Quaternion(const Quaternion &quaternion);
		


		Quaternion &operator=(const Quaternion &quaternion);

		double GetQ0() const { return m_q0; }
		double GetQ1() const { return m_q1; }
		double GetQ2() const { return m_q2; }
		double GetQ3() const { return m_q3; }

		/**
		 * @brief Multiply quaternion
		 * 
		 * @param quaternion 
		 * @return IO::SDK::Math::Quaternion 
		 */
		IO::SDK::Math::Quaternion Multiply(const Quaternion &quaternion) const;

		/**
		 * @brief Multiply quaternion
		 * 
		 * @param quaternion 
		 * @return IO::SDK::Math::Quaternion 
		 */
		IO::SDK::Math::Quaternion operator*(const Quaternion &quaternion) const;

		/**
		 * @brief Get the rotation matrix
		 * 
		 * @return IO::SDK::Math::Matrix 
		 */
		IO::SDK::Math::Matrix GetMatrix() const;

		/**
		 * @brief Get the magnitude of the quaternion
		 * 
		 * @return double 
		 */
		double Magnitude() const;

		/**
		 * @brief Normalize the quaternion
		 * 
		 * @return Quaternion 
		 */
		Quaternion Normalize() const;

		/**
		 * @brief Conjugate the quaternion
		 * 
		 */
		Quaternion Conjugate() const;
	};
}
#endif // !QUATERNION_H
