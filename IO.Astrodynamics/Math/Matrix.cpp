/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Matrix.h>
#include <InvalidArgumentException.h>

IO::Astrodynamics::Math::Matrix::Matrix(const Matrix &v) : Matrix(v.m_rowSize, v.m_colSize)
{
    if(v.m_data== nullptr)
    {
        throw Exception::InvalidArgumentException("Matrix has null pointer");
    }
	for (std::size_t i = 0; i < v.m_rowSize; i++)
	{
		for (std::size_t j = 0; j < v.m_colSize; j++)
		{
			m_data[i][j] = v.m_data[i][j];
		}
	}
}

IO::Astrodynamics::Math::Matrix::~Matrix()
{
	for (std::size_t i = 0; i < m_rowSize; i++)
		delete[] this->m_data[i];

	delete[] m_data;
}

double IO::Astrodynamics::Math::Matrix::GetValue(const size_t rowIdx, const size_t colIdx) const
{
	if (rowIdx >= m_rowSize)
	{
		throw IO::Astrodynamics::Exception::SDKException("Row index is out of range");
	}
	else if (colIdx >= m_colSize)
	{
		throw IO::Astrodynamics::Exception::SDKException("Column index is out of range");
	}

	return this->m_data[rowIdx][colIdx];
}

void IO::Astrodynamics::Math::Matrix::SetValue(const size_t rowIdx, const size_t colIdx, double value)
{
	if (rowIdx >= m_rowSize)
	{
		throw IO::Astrodynamics::Exception::SDKException("Row index is out of range");
	}
	else if (colIdx >= m_colSize)
	{
		throw IO::Astrodynamics::Exception::SDKException("Column index is out of range");
	}
	this->m_data[rowIdx][colIdx] = value;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Math::Matrix::Multiply(const Matrix &matrix)
{
	if (m_colSize != matrix.m_rowSize)
	{
		throw IO::Astrodynamics::Exception::SDKException("Matrixes with incompatible size");
	}

	Matrix res(m_rowSize, matrix.m_colSize);

	for (size_t i = 0; i < m_rowSize; i++)
	{
		for (size_t j = 0; j < matrix.m_colSize; j++)
		{
			double sum{};
			for (size_t k = 0; k < m_colSize; k++)
			{
				sum += m_data[i][k] * matrix.m_data[k][j];
			}
			res.m_data[i][j] = sum;
		}
	}

	return res;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Math::Matrix::Transpose() const
{
	//Row and columns size are switched
	Matrix res(m_colSize, m_rowSize);

	for (size_t i = 0; i < m_rowSize; i++)
	{
		for (size_t j = 0; j < m_colSize; j++)
		{
			res.m_data[i][j] = m_data[j][i];
		}
	}

	return res;
}

double **IO::Astrodynamics::Math::Matrix::GetRawData() const
{
	return m_data;
}