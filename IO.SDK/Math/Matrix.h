/**
 * @file Matrix.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-12
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef MATRIX_H
#define MATRIX_H
#include<cstddef>
#include<cstring>

namespace IO::SDK::Math
{
	/// <summary>
	/// Matrix
	/// </summary>
	class Matrix
	{
	private:
		double** m_data{ nullptr };
		const std::size_t m_colSize{};
		const std::size_t m_rowSize{};

	public:

		/// <summary>
		/// Instanciate sized matix
		/// </summary>
		/// <param name="rowSize"></param>
		/// <param name="colSize"></param>
		Matrix(const std::size_t rowSize, const std::size_t colSize) :m_colSize{ colSize }, m_rowSize{ rowSize }
		{
			this->m_data = new double* [rowSize];

			for (std::size_t i = 0; i < rowSize; i++)
			{
				this->m_data[i] = new double[colSize] {};
			}
		}

		/// <summary>
		/// Instanciate matrix with data
		/// </summary>
		/// <param name="rowSize"></param>
		/// <param name="colSize"></param>
		/// <param name="data"></param>
		Matrix(const std::size_t rowSize, const std::size_t colSize, double** data) :Matrix{ rowSize, colSize }
		{
			for (size_t i = 0; i < rowSize; i++)
			{
				for (size_t j = 0; j < colSize; j++)
				{
					m_data[i][j] = data[i][j];
				}
			}
		}
		Matrix(const Matrix& v);
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
		[[nodiscard]] std::size_t GetColumsSize() const { return m_colSize; }

		/// <summary>
		/// Get the matirx rows size
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] std::size_t GetRowsSize() const { return m_rowSize; }

		/// <summary>
		/// Multiply this matrix by another
		/// </summary>
		/// <param name="matrix"></param>
		/// <returns></returns>
		Matrix Multiply(const Matrix& matrix);

		/// <summary>
		/// Transpose this matrix
		/// </summary>
		/// <returns></returns>
		[[nodiscard]] Matrix Transpose() const;

		[[nodiscard]] double** GetRawData() const;
	};
}
#endif // !MATRIX_H

