#include <cmath>
#include<gtest/gtest.h>
#include<Frames.h>
#include<TDB.h>

TEST(Frames, ToTEME)
{
    IO::Astrodynamics::Time::TDB epoch("2021-Jan-01 00:00:00.0000 TDB");
    auto mtx = IO::Astrodynamics::Frames::Frames::ToTEME(epoch);
    auto identity = mtx.Multiply(mtx.Transpose());
    ASSERT_TRUE(identity.IsIdentity());
    ASSERT_NEAR(mtx.Determinant3X3(), 1.0, 1e-12);
}


TEST(TDB, ExtractTime)
{
    int year, month, day, hour, minute;
    double second;
    IO::Astrodynamics::Frames::Frames::ExtractDateTimeComponents("2021-02-03 13:14:15.60 TDB", year, month, day,
                                                                 hour, minute, second);

    ASSERT_EQ(2021, year);
    ASSERT_EQ(2, month);
    ASSERT_EQ(3, day);
    ASSERT_EQ(13, hour);
    ASSERT_EQ(14, minute);
    ASSERT_DOUBLE_EQ(15.60, second);
}
