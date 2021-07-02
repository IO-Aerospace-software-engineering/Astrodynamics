#include <gtest/gtest.h>
#include <SDKException.h>
#include <TooEarlyManeuverException.h>
#include <PropagatorException.h>

TEST(Exceptions, SDKException)
{
    IO::SDK::Exception::SDKException ex("Test");
    ASSERT_STREQ("Test", ex.what());
}

TEST(Exceptions, TooEarlyManeuverException)
{
    IO::SDK::Exception::TooEarlyManeuverException ex("Test");
    ASSERT_STREQ("Test", ex.what());
}

TEST(Exceptions, PropagatorException)
{
    IO::SDK::Exception::PropagatorException ex("Test");
    ASSERT_STREQ("Test", ex.what());
}