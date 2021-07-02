#include <gtest/gtest.h>
#include <Coordinate.h>

TEST(Coordinate, ToCharArray)
{
    ASSERT_STREQ("X", IO::SDK::Coordinate::X.ToCharArray());
}