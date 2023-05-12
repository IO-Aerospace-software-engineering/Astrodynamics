/**
 * @file Quaternion.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Quaternion.h>
#include <cmath>
#include <SpiceUsr.h>

IO::SDK::Math::Quaternion::Quaternion()
= default;

IO::SDK::Math::Quaternion::Quaternion(double q0, double q1, double q2, double q3) : m_q0{q0}, m_q1{q1}, m_q2{q2}, m_q3{q3}
{
}

IO::SDK::Math::Quaternion::Quaternion(const IO::SDK::Math::Vector3D &axis, const double angle)
{
	double c{std::cos(angle / 2)};
	double s{std::sin(angle / 2)};
	const_cast<double &>(m_q0) = c;
	const_cast<double &>(m_q1) = s * axis.GetX();
	const_cast<double &>(m_q2) = s * axis.GetY();
	const_cast<double &>(m_q3) = s * axis.GetZ();
}

IO::SDK::Math::Quaternion::Quaternion(const IO::SDK::Math::Matrix &mtx)
{
	SpiceDouble m[3][3]{};

	for (size_t i = 0; i < 3; i++)
	{
		for (size_t j = 0; j < 3; j++)
		{
			m[i][j] = mtx.GetValue(i, j);
		}
	}

	SpiceDouble q[4];
	m2q_c(m, q);

	const_cast<double &>(m_q0) = q[0];
	const_cast<double &>(m_q1) = q[1];
	const_cast<double &>(m_q2) = q[2];
	const_cast<double &>(m_q3) = q[3];
}

IO::SDK::Math::Quaternion::Quaternion(const IO::SDK::Math::Quaternion &quaternion):Quaternion(quaternion.GetQ0(),quaternion.GetQ1(),quaternion.GetQ2(),quaternion.GetQ3())
{

}

IO::SDK::Math::Quaternion IO::SDK::Math::Quaternion::Multiply(const Quaternion &quaternion) const
{
	return *this * quaternion;
}

IO::SDK::Math::Quaternion IO::SDK::Math::Quaternion::operator*(const Quaternion &quaternion) const
{
	ConstSpiceDouble _this[4] = {m_q0, m_q1, m_q2, m_q3};
	ConstSpiceDouble other[4] = {quaternion.m_q0, quaternion.m_q1, quaternion.m_q2, quaternion.m_q3};
	SpiceDouble res[4];
	qxq_c(_this, other, res);
	return Quaternion{res[0], res[1], res[2], res[3]};
}

IO::SDK::Math::Matrix IO::SDK::Math::Quaternion::GetMatrix() const
{
	SpiceDouble mtx[3][3];
	ConstSpiceDouble q[4] = {m_q0, m_q1, m_q2, m_q3};
	q2m_c(q, mtx);

	double **exportMtx = new double *[3];
	for (int i = 0; i < 3; i++)
	{
		exportMtx[i] = new double[3]{};
	}

	for (size_t i = 0; i < 3; i++)
	{
		for (size_t j = 0; j < 3; j++)
		{
			exportMtx[i][j] = mtx[i][j];
		}
	}

	return IO::SDK::Math::Matrix{3, 3, exportMtx};
}

double IO::SDK::Math::Quaternion::Magnitude() const
{
	return std::sqrt(m_q0 * m_q0 + m_q1 * m_q1 + m_q2 * m_q2 + m_q3 * m_q3);
}

IO::SDK::Math::Quaternion IO::SDK::Math::Quaternion::Normalize() const
{
	auto magnitude = Magnitude();
	return IO::SDK::Math::Quaternion{m_q0 / magnitude, m_q1 / magnitude, m_q2 / magnitude, m_q3 / magnitude};
}

IO::SDK::Math::Quaternion IO::SDK::Math::Quaternion::Conjugate() const
{
	return IO::SDK::Math::Quaternion{m_q0, -m_q1, -m_q2, -m_q3};
}

IO::SDK::Math::Quaternion &IO::SDK::Math::Quaternion::operator=(const IO::SDK::Math::Quaternion &quaternion)
{
	if (this != &quaternion) // not a self-assignment
	{
		const_cast<double &>(m_q0) = quaternion.m_q0;
		const_cast<double &>(m_q1) = quaternion.m_q1;
		const_cast<double &>(m_q2) = quaternion.m_q2;
		const_cast<double &>(m_q3) = quaternion.m_q3;
	}
	return *this;
}