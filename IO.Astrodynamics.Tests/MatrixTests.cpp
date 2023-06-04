#include<gtest/gtest.h>
#include<Matrix.h>
#include<SDKException.h>

TEST(Matrix, Initialization)
{
	IO::Astrodynamics::Math::Matrix mat(3, 4);
	ASSERT_EQ(3, mat.GetRowsSize());
	ASSERT_EQ(4, mat.GetColumsSize());
	ASSERT_EQ(0.0, mat.GetValue(0, 0));
	ASSERT_EQ(0.0, mat.GetValue(2, 3));


	double array[3][3]{ {0,1,2},{3,4,5},{6,7,8} };

	double** arrayCmat;
	arrayCmat = new double* [3];
	for (int i = 0; i < 3; i++)
		arrayCmat[i] = new double[3]{};

	for (size_t i = 0; i < 3; i++)
	{
		for (size_t j = 0; j < 3; j++)
		{
			arrayCmat[i][j] = array[i][j];
		}
	}

	IO::Astrodynamics::Math::Matrix matFromArr(3, 3, arrayCmat);

	for (size_t i = 0; i < 3; i++)
	{
		for (size_t j = 0; j < 3; j++)
		{
			ASSERT_DOUBLE_EQ(i * 3 + j, matFromArr.GetValue(i, j));
		}
	}
}

TEST(Matrix, SetValue)
{
	IO::Astrodynamics::Math::Matrix mat(3, 4);
	mat.SetValue(1, 2, 54.0);
	ASSERT_EQ(54.0, mat.GetValue(1, 2));

}

TEST(Matrix, OutOfrange)
{
	IO::Astrodynamics::Math::Matrix mat(3, 4);
	ASSERT_THROW(mat.SetValue(3, 2, 3.0), IO::Astrodynamics::Exception::SDKException);
	ASSERT_THROW(mat.SetValue(2, 4, 5.0), IO::Astrodynamics::Exception::SDKException);
	ASSERT_THROW(mat.GetValue(3, 2), IO::Astrodynamics::Exception::SDKException);
	ASSERT_THROW(mat.GetValue(2, 4), IO::Astrodynamics::Exception::SDKException);
}

TEST(Matrix, Multiply)
{
	IO::Astrodynamics::Math::Matrix mat(2, 2);
	IO::Astrodynamics::Math::Matrix mat2(2, 2);
	mat.SetValue(0, 0, 2.0);
	mat.SetValue(0, 1, 3.0);
	mat.SetValue(1, 0, 4.0);
	mat.SetValue(1, 1, 5.0);

	mat2.SetValue(0, 0, 6.0);
	mat2.SetValue(0, 1, 7.0);
	mat2.SetValue(1, 0, 8.0);
	mat2.SetValue(1, 1, 9.0);

	auto res = mat.Multiply(mat2);

	ASSERT_DOUBLE_EQ(36.0, res.GetValue(0, 0));
	ASSERT_DOUBLE_EQ(41.0, res.GetValue(0, 1));
	ASSERT_DOUBLE_EQ(64.0, res.GetValue(1, 0));
	ASSERT_DOUBLE_EQ(73.0, res.GetValue(1, 1));

}

TEST(Matrix, Transpose)
{
	IO::Astrodynamics::Math::Matrix mat(2, 2);
	mat.SetValue(0, 0, 2.0);
	mat.SetValue(0, 1, 3.0);
	mat.SetValue(1, 0, 4.0);
	mat.SetValue(1, 1, 5.0);

	auto res = mat.Transpose();

	ASSERT_DOUBLE_EQ(2.0, res.GetValue(0, 0));
	ASSERT_DOUBLE_EQ(4.0, res.GetValue(0, 1));
	ASSERT_DOUBLE_EQ(3.0, res.GetValue(1, 0));
	ASSERT_DOUBLE_EQ(5.0, res.GetValue(1, 1));

}

TEST(Matrix, Copy)
{
	IO::Astrodynamics::Math::Matrix mat(2, 2);
	mat.SetValue(0, 0, 2.0);
	mat.SetValue(0, 1, 3.0);
	mat.SetValue(1, 0, 4.0);
	mat.SetValue(1, 1, 5.0);

	IO::Astrodynamics::Math::Matrix mat2(mat);

	auto raw=mat.GetRawData();
	auto raw2=mat2.GetRawData();
	
for (size_t i = 0; i < 2; i++)
{
	for (size_t j = 0; j < 2; j++)
	{
		ASSERT_DOUBLE_EQ(raw[i][j],raw2[i][j]);
	}
	
}

}