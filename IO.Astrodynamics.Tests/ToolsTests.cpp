/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <Tools.h>


TEST(Tools, AngleDifference1)
{
    auto res = AngleDifference(1, 3);
    ASSERT_DOUBLE_EQ(2.0, res);
}

TEST(Tools, AngleDifference2)
{
    auto res = AngleDifference(3, 1);
    ASSERT_DOUBLE_EQ(2.0, res);
}

TEST(Tools, AngleDifference3)
{
    auto res = AngleDifference(6.27, 0.1);
    ASSERT_DOUBLE_EQ(0.1131853071795863, res);
}

TEST(Tools, AngleDifference4)
{
    auto res = AngleDifference(0.1, 6.27);
    ASSERT_DOUBLE_EQ(0.1131853071795863, res);
}

TEST(Tools, AngleDifference5)
{
    auto res = AngleDifference(-1, -1);
    ASSERT_DOUBLE_EQ(0.0, res);
}

TEST(Tools, AngleDifference6)
{
    auto res = AngleDifference(-1, 1);
    ASSERT_DOUBLE_EQ(2.0, res);
}

TEST(Tools, AngleDifference7)
{
    auto res = AngleDifference(1, -1);
    ASSERT_DOUBLE_EQ(2.0, res);
}

TEST(Tools, AngleDifference8)
{
    auto res = AngleDifference(IO::Astrodynamics::Constants::PI2, IO::Astrodynamics::Constants::PI2*3.0);
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Constants::PI, res);
}
TEST(Tools, AngleDifference9)
{
    auto res = AngleDifference(IO::Astrodynamics::Constants::PI2*3.0, IO::Astrodynamics::Constants::PI2);
    ASSERT_DOUBLE_EQ(IO::Astrodynamics::Constants::PI, res);
}

TEST(Tools, AngleDifference10)
{
    auto res = AngleDifference(120.0, 121.0);
    ASSERT_DOUBLE_EQ(1.0, res);
}

