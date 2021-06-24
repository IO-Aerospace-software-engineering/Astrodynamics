#include <Vector3D.h>
#include <cmath>
#include <SDKException.h>
#include <Quaternion.h>
#include <Constants.h>

const IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::VectorX{1.0, 0.0, 0.0};
const IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::VectorY{0.0, 1.0, 0.0};
const IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::VectorZ{0.0, 0.0, 1.0};

double IO::SDK::Math::Vector3D::Magnitude() const
{
	return std::sqrt(m_x * m_x + m_y * m_y + m_z * m_z);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator+(const Vector3D &vector) const
{
	return Vector3D(m_x + vector.m_x, m_y + vector.m_y, m_z + vector.m_z);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator-(const Vector3D &vector) const
{
	return Vector3D(m_x - vector.m_x, m_y - vector.m_y, m_z - vector.m_z);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator*(const double value) const
{
	return Vector3D(m_x * value, m_y * value, m_z * value);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator/(const double value) const
{
	return Vector3D(m_x / value, m_y / value, m_z / value);
}

IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::CrossProduct(const Vector3D &vector) const
{
	return Vector3D(m_y * vector.m_z - m_z * vector.m_y, m_z * vector.m_x - m_x * vector.m_z, m_x * vector.m_y - m_y * vector.m_x);
}

double IO::SDK::Math::Vector3D::DotProduct(const Vector3D &vector) const
{
	return m_x * vector.m_x + m_y * vector.m_y + m_z * vector.m_z;
}

IO::SDK::Math::Vector3D &IO::SDK::Math::Vector3D::operator=(const Vector3D &vector)
{
	if (this == &vector)
	{
		return *this;
	}

	const_cast<double &>(m_x) = vector.m_x;
	const_cast<double &>(m_y) = vector.m_y;
	const_cast<double &>(m_z) = vector.m_z;

	return *this;
}

IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::Normalize() const
{
	if (Magnitude() == 0)
	{
		throw IO::SDK::Exception::SDKException("Vector must have magnitude");
	}
	return (*this) / Magnitude();
}

double IO::SDK::Math::Vector3D::GetAngle(const Vector3D &vector) const
{
	return std::acos(DotProduct(vector) / (Magnitude() * vector.Magnitude()));
}

bool IO::SDK::Math::Vector3D::operator==(const IO::SDK::Math::Vector3D &vector) const
{
	return m_x == vector.GetX() && m_y == vector.GetY() && m_z == vector.GetZ();
}

IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::Rotate(const IO::SDK::Math::Quaternion &quaternion) const
{
	// Extract the vector part of the quaternion
	IO::SDK::Math::Vector3D u{quaternion.GetQ1(), quaternion.GetQ2(), quaternion.GetQ3()};

	// Extract the scalar part of the quaternion
	float s = quaternion.GetQ0();

	// Do the math
	IO::SDK::Math::Vector3D vprime = u * 2.0f * u.DotProduct(*this) + (*this) * (s * s - u.DotProduct(u)) + u.CrossProduct(*this) * 2.0f * s;

	return vprime;
}

IO::SDK::Math::Quaternion IO::SDK::Math::Vector3D::To(const Vector3D &vector) const
{
	auto dot = this->DotProduct(vector);

	if (dot == -1.0)//Manage 180° case
	{
		float x = std::abs(vector.GetX());
		float y = std::abs(vector.GetY());
		float z = std::abs(vector.GetZ());

		IO::SDK::Math::Vector3D axis = x < y ? (x < z ? IO::SDK::Math::Vector3D::VectorX : IO::SDK::Math::Vector3D::VectorZ) : (y < z ? IO::SDK::Math::Vector3D::VectorY : IO::SDK::Math::Vector3D::VectorZ);
		auto v = vector.CrossProduct(axis);
		return IO::SDK::Math::Quaternion(0.0, v.GetX(), v.GetY(), v.GetZ());
	}

	auto mag1 = this->Magnitude();
	auto mag2 = vector.Magnitude();
	auto v = this->CrossProduct(vector);
	auto w = dot + std::sqrt(mag1 * mag1 * mag2 * mag2);

	return IO::SDK::Math::Quaternion(w, v.GetX(), v.GetY(), v.GetZ());
}

IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::Reverse() const
{
	return *this * -1.0;
}