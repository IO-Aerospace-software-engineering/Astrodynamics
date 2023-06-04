/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include <gtest/gtest.h>
#include <AzimuthRange.h>

TEST(AzimuthRange, Initialization)
{
    IO::SDK::Coordinates::AzimuthRange az(4.0, 6.0);

    ASSERT_DOUBLE_EQ(4.0, az.GetStart());
    ASSERT_DOUBLE_EQ(6.0, az.GetEnd());
    ASSERT_DOUBLE_EQ(2.0, az.GetSpan());
}

TEST(AzimuthRange, Span1)
{
    IO::SDK::Coordinates::AzimuthRange az(6.0, 2.0);

    ASSERT_DOUBLE_EQ(6.0, az.GetStart());
    ASSERT_DOUBLE_EQ(2.0, az.GetEnd());
    ASSERT_DOUBLE_EQ(2.2831853071795862, az.GetSpan());
}

TEST(AzimuthRange, Span2)
{
    IO::SDK::Coordinates::AzimuthRange az(2.0, 1.0);

    ASSERT_DOUBLE_EQ(2.0, az.GetStart());
    ASSERT_DOUBLE_EQ(1.0, az.GetEnd());
    ASSERT_DOUBLE_EQ(5.2831853071795862, az.GetSpan());
}

TEST(AzimuthRange, Span3)
{
    IO::SDK::Coordinates::AzimuthRange az(-1.0, 4.0);

    ASSERT_DOUBLE_EQ(5.2831853071795862, az.GetStart());
    ASSERT_DOUBLE_EQ(4.0, az.GetEnd());
    ASSERT_DOUBLE_EQ(5.0, az.GetSpan());
}

TEST(AzimuthRange, IsInRange)
{
    IO::SDK::Coordinates::AzimuthRange az(4.0, 6.0);

    ASSERT_TRUE(az.IsInRange(4.0));
    ASSERT_TRUE(az.IsInRange(5.0));
    ASSERT_TRUE(az.IsInRange(6.0));
    ASSERT_FALSE(az.IsInRange(3.9));
    ASSERT_FALSE(az.IsInRange(6.1));
    ASSERT_FALSE(az.IsInRange(1.0));
}

TEST(AzimuthRange, IsInRange2)
{
    IO::SDK::Coordinates::AzimuthRange az2(6.0, 1.0);

    ASSERT_TRUE(az2.IsInRange(6.0));
    ASSERT_TRUE(az2.IsInRange(0.1));
    ASSERT_TRUE(az2.IsInRange(1.0));
    ASSERT_FALSE(az2.IsInRange(1.1));
    ASSERT_FALSE(az2.IsInRange(5.9));
    ASSERT_FALSE(az2.IsInRange(3.0));
}

TEST(AzimuthRange, IsInRange3)
{
    IO::SDK::Coordinates::AzimuthRange az2(1.0, 3.0);

    ASSERT_TRUE(az2.IsInRange(1.0));
    ASSERT_TRUE(az2.IsInRange(3.0));
    ASSERT_FALSE(az2.IsInRange(0.9));
    ASSERT_FALSE(az2.IsInRange(3.1));
    ASSERT_FALSE(az2.IsInRange(4.0));
}

TEST(AzimuthRange, IsInRange4)
{
    IO::SDK::Coordinates::AzimuthRange az2(1.0, 5.0);

    ASSERT_TRUE(az2.IsInRange(1.0));
    ASSERT_TRUE(az2.IsInRange(5.0));
    ASSERT_TRUE(az2.IsInRange(3.0));
    ASSERT_FALSE(az2.IsInRange(5.1));
    ASSERT_FALSE(az2.IsInRange(0.0));
    ASSERT_FALSE(az2.IsInRange(0.9));
}

TEST(AzimuthRange, IsInRange5)
{
    IO::SDK::Coordinates::AzimuthRange az2(4.0, 3.0);

    ASSERT_TRUE(az2.IsInRange(4.0));
    ASSERT_TRUE(az2.IsInRange(0.0));
    ASSERT_TRUE(az2.IsInRange(3.0));
    ASSERT_FALSE(az2.IsInRange(3.1));
    ASSERT_FALSE(az2.IsInRange(3.5));
    ASSERT_FALSE(az2.IsInRange(3.9));
}

TEST(AzimuthRange, IsIntersected)
{
    IO::SDK::Coordinates::AzimuthRange az2(2.0, 5.0);

    ASSERT_TRUE(az2.IsIntersected(IO::SDK::Coordinates::AzimuthRange(1.0,3.0)));
    ASSERT_TRUE(az2.IsIntersected(IO::SDK::Coordinates::AzimuthRange(1.0,6.0)));
    ASSERT_TRUE(az2.IsIntersected(IO::SDK::Coordinates::AzimuthRange(3.0,6.0)));
    ASSERT_TRUE(az2.IsIntersected(IO::SDK::Coordinates::AzimuthRange(3.0,4.0)));

    ASSERT_FALSE(az2.IsIntersected(IO::SDK::Coordinates::AzimuthRange(1.0,1.99)));
    ASSERT_FALSE(az2.IsIntersected(IO::SDK::Coordinates::AzimuthRange(5.1,6.0)));
}