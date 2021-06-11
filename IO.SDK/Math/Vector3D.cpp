#include <Vector3D.h>
#include <cmath>
#include <SDKException.h>
#include <Quaternion.h>

const IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::VectorX{1.0,0.0,0.0};
const IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::VectorY{0.0,1.0,0.0};
const IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::VectorZ{0.0,0.0,1.0};

double IO::SDK::Math::Vector3D::Magnitude() const
{
	return std::sqrt(_x * _x + _y * _y + _z * _z);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator+(const Vector3D &vector) const
{
	return Vector3D(_x + vector._x, _y + vector._y, _z + vector._z);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator-(const Vector3D &vector) const
{
	return Vector3D(_x - vector._x, _y - vector._y, _z - vector._z);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator*(const double value) const
{
	return Vector3D(_x * value, _y * value, _z * value);
}

IO::SDK::Math::Vector3D const IO::SDK::Math::Vector3D::operator/(const double value) const
{
	return Vector3D(_x / value, _y / value, _z / value);
}

IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::CrossProduct(const Vector3D &vector) const
{
	return Vector3D(_y * vector._z - _z * vector._y, _z * vector._x - _x * vector._z, _x * vector._y - _y * vector._x);
}

double IO::SDK::Math::Vector3D::DotProduct(const Vector3D &vector) const
{
	return _x * vector._x + _y * vector._y + _z * vector._z;
}

IO::SDK::Math::Vector3D &IO::SDK::Math::Vector3D::operator=(const Vector3D &vector)
{
	if (this == &vector)
	{
		return *this;
	}

	const_cast<double &>(_x) = vector._x;
	const_cast<double &>(_y) = vector._y;
	const_cast<double &>(_z) = vector._z;

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
	return _x == vector.GetX() && _y == vector.GetY() && _z == vector.GetZ();
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
	auto mag1 = this->Magnitude();
	auto mag2 = vector.Magnitude();
	auto v = this->CrossProduct(vector);
	auto w = this->DotProduct(vector) + std::sqrt(mag1 * mag1 * mag2 * mag2);
	return IO::SDK::Math::Quaternion(w, v.GetX(), v.GetY(), v.GetZ());
}

IO::SDK::Math::Vector3D IO::SDK::Math::Vector3D::Reverse() const
{
	return *this * -1.0;
}