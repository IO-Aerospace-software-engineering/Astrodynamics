/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef MATRIX_H
#define MATRIX_H

#include<cstddef>
#include<string>
#include <Vector3D.h>

namespace IO::Astrodynamics::Math
{
    /// <summary>
    /// Matrix
    /// </summary>
    class Matrix
    {
    private:
        double **m_data{nullptr};
        const std::size_t m_colSize{};
        const std::size_t m_rowSize{};
        const double m_tolerance{1e-12};

    public:
        /// <summary>
        /// Instantiate sized matrix
        /// </summary>
        /// <param name="rowSize"></param>
        /// <param name="colSize"></param>
        Matrix(const std::size_t rowSize, const std::size_t colSize) : m_colSize{colSize}, m_rowSize{rowSize}
        {
            this->m_data = new double *[rowSize];

            for (std::size_t i = 0; i < rowSize; i++)
            {
                this->m_data[i] = new double[colSize]{};
            }
        }

        /// <summary>
        /// Instantiate matrix with data
        /// </summary>
        /// <param name="rowSize"></param>
        /// <param name="colSize"></param>
        /// <param name="data"></param>
        Matrix(const std::size_t rowSize, const std::size_t colSize, double **data) : Matrix{rowSize, colSize}
        {
            for (size_t i = 0; i < rowSize; i++)
            {
                for (size_t j = 0; j < colSize; j++)
                {
                    m_data[i][j] = data[i][j];
                }
            }
        }

        /// <summary>
        /// Instantiate a 3x3 matrix with given data
        /// </summary>
        /// <param name="data">2D array containing matrix data</param>
        explicit Matrix(double data[3][3]) : Matrix{3, 3}
        {
            for (size_t i = 0; i < 3; i++)
            {
                for (size_t j = 0; j < 3; j++)
                {
                    m_data[i][j] = data[i][j];
                }
            }
        }

        Matrix(const Matrix &v);

        ~Matrix();

        /// <summary>
        /// Get value at given index
        /// </summary>
        /// <param name="rowIdx">Row index</param>
        /// <param name="colIdx">Column index</param>
        /// <returns></returns>
        [[nodiscard]] double GetValue(std::size_t rowIdx, std::size_t colIdx) const;

        /// <summary>
        /// Set value at given index
        /// </summary>
        /// <param name="rowIdx">Row index</param>
        /// <param name="colIdx">Column index</param>
        /// <param name="value">Value index</param>
        void SetValue(std::size_t rowIdx, std::size_t colIdx, double value);

        /// <summary>
        /// Get the matrix columns size
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] std::size_t GetColumnsSize() const
        { return m_colSize; }

        /// <summary>
        /// Get the matirx rows size
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] std::size_t GetRowsSize() const
        { return m_rowSize; }

        /// <summary>
        /// Multiply this matrix by another
        /// </summary>
        /// <param name="matrix"></param>
        /// <returns></returns>
        Matrix Multiply(const Matrix &matrix);

        Vector3D Multiply(const Vector3D &matrix);

        /// <summary>
        /// Transpose this matrix
        /// </summary>
        /// <returns></returns>
        [[nodiscard]] Matrix Transpose() const;

        [[nodiscard]] double **GetRawData() const;

        [[nodiscard]] bool IsIdentity() const;

        [[nodiscard]]double Determinant3X3() const;

        [[nodiscard]] std::string ToString() const;
    };
}
#endif // !MATRIX_H
