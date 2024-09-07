#include <cmath>
#include <Constants.h>
#include<gtest/gtest.h>
#include<Frames.h>
#include<TDB.h>

TEST(Frames, ToTEME)
{
    IO::Astrodynamics::Time::TDB epoch("2000-Jan-01 00:00:00.0000 TDB");
    auto mtx = IO::Astrodynamics::Frames::Frames::ToTEME(epoch);
    auto identity = mtx.Multiply(mtx.Transpose());
    ASSERT_TRUE(identity.IsIdentity());
    ASSERT_NEAR(mtx.Determinant3X3(), 1.0, 1e-12);

    double p[3];
    radrec_c(1000000.0,331.5907*IO::Astrodynamics::Constants::DEG_RAD,11.85897*IO::Astrodynamics::Constants::DEG_RAD,p);
    IO::Astrodynamics::Math::Vector3D v(p[0], p[1], p[2]);
    auto v2 = mtx.Multiply(v);

    double v2Array[3];
    v2Array[0] = v2.GetX();
    v2Array[1] = v2.GetY();
    v2Array[2] = v2.GetZ();
    double radius,ra,dec;
    recrad_c(v2Array,&radius,&ra,&dec);
    ra=ra*IO::Astrodynamics::Constants::RAD_DEG;
    dec=dec*IO::Astrodynamics::Constants::RAD_DEG;

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
