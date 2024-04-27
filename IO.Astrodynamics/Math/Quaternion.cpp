/*
 Copyright (c) 2021-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Quaternion.h>
#include <cmath>
#include <SpiceUsr.h>

IO::Astrodynamics::Math::Quaternion::Quaternion()
= default;

IO::Astrodynamics::Math::Quaternion::Quaternion(double q0, double q1, double q2, double q3) : m_q0{q0}, m_q1{q1}, m_q2{q2}, m_q3{q3}
{
}

IO::Astrodynamics::Math::Quaternion::Quaternion(const IO::Astrodynamics::Math::Vector3D &axis, const double angle)
{
    double c{std::cos(angle / 2)};
    double s{std::sin(angle / 2)};
    const_cast<double &>(m_q0) = c;
    const_cast<double &>(m_q1) = s * axis.GetX();
    const_cast<double &>(m_q2) = s * axis.GetY();
    const_cast<double &>(m_q3) = s * axis.GetZ();
}

IO::Astrodynamics::Math::Quaternion::Quaternion(const IO::Astrodynamics::Math::Matrix &mtx)
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

IO::Astrodynamics::Math::Quaternion::Quaternion(const IO::Astrodynamics::Math::Quaternion &quaternion) : Quaternion(quaternion.GetQ0(), quaternion.GetQ1(), quaternion.GetQ2(),
                                                                                                                    quaternion.GetQ3())
{

}

IO::Astrodynamics::Math::Quaternion IO::Astrodynamics::Math::Quaternion::Multiply(const Quaternion &quaternion) const
{
    return *this * quaternion;
}

IO::Astrodynamics::Math::Quaternion IO::Astrodynamics::Math::Quaternion::operator*(const Quaternion &quaternion) const
{
    ConstSpiceDouble _this[4] = {m_q0, m_q1, m_q2, m_q3};
    ConstSpiceDouble other[4] = {quaternion.m_q0, quaternion.m_q1, quaternion.m_q2, quaternion.m_q3};
    SpiceDouble res[4];
    qxq_c(_this, other, res);
    return Quaternion{res[0], res[1], res[2], res[3]};
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Math::Quaternion::GetMatrix() const
{
    SpiceDouble mtx[3][3];
    ConstSpiceDouble q[4] = {m_q0, m_q1, m_q2, m_q3};
    q2m_c(q, mtx);
    double exportMtx_data[3][3]{};
    double *exportMtx[3];

// Assign pointers to the rows
    for (int i = 0; i < 3; ++i)
    {
        exportMtx[i] = exportMtx_data[i];
    }

// Copy data from mtx to exportMtx
    for (size_t i = 0; i < 3; ++i)
    {
        for (size_t j = 0; j < 3; ++j)
        {
            exportMtx[i][j] = mtx[i][j];
        }
    }
    return IO::Astrodynamics::Math::Matrix{3, 3, exportMtx};
}

double IO::Astrodynamics::Math::Quaternion::Magnitude() const
{
    return std::sqrt(m_q0 * m_q0 + m_q1 * m_q1 + m_q2 * m_q2 + m_q3 * m_q3);
}

IO::Astrodynamics::Math::Quaternion IO::Astrodynamics::Math::Quaternion::Normalize() const
{
    auto magnitude = Magnitude();
    return IO::Astrodynamics::Math::Quaternion{m_q0 / magnitude, m_q1 / magnitude, m_q2 / magnitude, m_q3 / magnitude};
}

IO::Astrodynamics::Math::Quaternion IO::Astrodynamics::Math::Quaternion::Conjugate() const
{
    return IO::Astrodynamics::Math::Quaternion{m_q0, -m_q1, -m_q2, -m_q3};
}

IO::Astrodynamics::Math::Quaternion &IO::Astrodynamics::Math::Quaternion::operator=(const IO::Astrodynamics::Math::Quaternion &quaternion)
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