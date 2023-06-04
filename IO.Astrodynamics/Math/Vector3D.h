/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef VECTOR3D_H
#define VECTOR3D_H

namespace IO::SDK::Math {
    class Quaternion;

    class Vector3D {
    private:
        double const m_x{}, m_y{}, m_z{};

    public:
        static const Vector3D VectorX;
        static const Vector3D VectorY;
        static const Vector3D VectorZ;
        static const Vector3D Zero;

        Vector3D() = default;

        /// <summary>
        /// Instantiate a 3D vector
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        Vector3D(double x, double y, double z) : m_x{x}, m_y{y}, m_z{z} {};

        Vector3D(const Vector3D &vector) = default;

        Vector3D &operator=(const Vector3D &vector);

        ~Vector3D() = default;

        /// <summary>
        /// Get X
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetX() const { return this->m_x; }

        /// <summary>
        /// Get Y
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetY() const { return this->m_y; }

        /// <summary>
        /// Get Z
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double GetZ() const { return this->m_z; }

        /// <summary>
        /// Get the vector magnitude
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] double Magnitude() const;

        Vector3D operator+(const Vector3D &vector) const;

        Vector3D operator-(const Vector3D &vector) const;

        Vector3D operator*(double value) const;

        Vector3D operator/(double value) const;


        /// <summary>
        /// Get the cross product from another vector
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        [[nodiscard]] Vector3D CrossProduct(const Vector3D &vector) const;

        /// <summary>
        /// Get the dot product
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        [[nodiscard]] double DotProduct(const Vector3D &vector) const;

        /// <summary>
        /// Get this normalized vector
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] Vector3D Normalize() const;

        /// <summary>
        /// Get angle from another vector
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        [[nodiscard]] double GetAngle(const Vector3D &vector) const;

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
        [[nodiscard]] Vector3D Rotate(const IO::SDK::Math::Quaternion &quaternion) const;

        /**
         * @brief Return the quaternion to rotate vector to another
         *
         * @param vector
         * @return Quaternion
         */
        [[nodiscard]] Quaternion To(const Vector3D &vector) const;

        /**
         * @brief Reverse vector
         *
         * @return Vector3D
         */
        [[nodiscard]] Vector3D Reverse() const;
    };
}
#endif // !VECTOR3D_H
