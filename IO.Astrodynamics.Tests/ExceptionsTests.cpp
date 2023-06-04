#include <gtest/gtest.h>
#include <SDKException.h>
#include <TooEarlyManeuverException.h>
#include <PropagatorException.h>

TEST(Exceptions, SDKException)
{
    IO::Astrodynamics::Exception::SDKException ex("Test");
    ASSERT_STREQ("Test", ex.what());
}

TEST(Exceptions, TooEarlyManeuverException)
{
    IO::Astrodynamics::Exception::TooEarlyManeuverException ex("Test");
    ASSERT_STREQ("Test", ex.what());
}

TEST(Exceptions, PropagatorException)
{
    IO::Astrodynamics::Exception::PropagatorException ex("Test");
    ASSERT_STREQ("Test", ex.what());
}