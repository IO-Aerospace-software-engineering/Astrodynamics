#include <gtest/gtest.h>
#include <CoordinateSystem.h>

TEST(CoordinateSystem, ToCharArray)
{
    ASSERT_STREQ("PLANETOGRAPHIC", IO::SDK::CoordinateSystem::Planetographic.ToCharArray());
}