#include <gtest/gtest.h>
#include <CelestialBody.h>
#include <Type.h>

TEST(Helpers, TypeOf)
{
    IO::Astrodynamics::Body::CelestialBody cb(301);
    auto res = IO::Astrodynamics::Helpers::IsInstanceOf<IO::Astrodynamics::Body::CelestialBody>(&cb);
    ASSERT_TRUE(res);

    IO::Astrodynamics::Body::Body *body = &cb;
    res = IO::Astrodynamics::Helpers::IsInstanceOf<IO::Astrodynamics::Body::CelestialBody>(body);
    ASSERT_TRUE(res);
}