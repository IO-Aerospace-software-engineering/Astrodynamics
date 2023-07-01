/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef QUATERNION_H
#define QUATERNION_H

#include <Matrix.h>
#include <Vector3D.h>

namespace IO::Astrodynamics::Math
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
		Quaternion(double q0, double q1, double q2, double q3);

		/**
		 * @brief Construct a new Quaternion object
		 * 
		 * @param axis 
		 * @param angle 
		 */
		Quaternion(const IO::Astrodynamics::Math::Vector3D &axis, double angle);

		/**
		 * @brief Construct a new Quaternion object
		 * 
		 * @param mtx 
		 */
		explicit Quaternion(const IO::Astrodynamics::Math::Matrix &mtx);

		/**
		 * @brief Construct a new Quaternion object
		 * 
		 * @param quaternion 
		 */
		Quaternion(const Quaternion &quaternion);
		


		Quaternion &operator=(const Quaternion &quaternion);

		[[nodiscard]] inline double GetQ0() const { return m_q0; }
		[[nodiscard]] inline double GetQ1() const { return m_q1; }
		[[nodiscard]] inline double GetQ2() const { return m_q2; }
		[[nodiscard]] inline double GetQ3() const { return m_q3; }

		/**
		 * @brief Multiply quaternion
		 * 
		 * @param quaternion 
		 * @return IO::Astrodynamics::Math::Quaternion
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Quaternion Multiply(const Quaternion &quaternion) const;

		/**
		 * @brief Multiply quaternion
		 * 
		 * @param quaternion 
		 * @return IO::Astrodynamics::Math::Quaternion
		 */
		IO::Astrodynamics::Math::Quaternion operator*(const Quaternion &quaternion) const;

		/**
		 * @brief Get the rotation matrix
		 * 
		 * @return IO::Astrodynamics::Math::Matrix
		 */
		[[nodiscard]] IO::Astrodynamics::Math::Matrix GetMatrix() const;

		/**
		 * @brief Get the magnitude of the quaternion
		 * 
		 * @return double 
		 */
		[[nodiscard]] double Magnitude() const;

		/**
		 * @brief Normalize the quaternion
		 * 
		 * @return Quaternion 
		 */
		[[nodiscard]] Quaternion Normalize() const;

		/**
		 * @brief Conjugate the quaternion
		 * 
		 */
		[[nodiscard]] Quaternion Conjugate() const;
	};
}
#endif // !QUATERNION_H
