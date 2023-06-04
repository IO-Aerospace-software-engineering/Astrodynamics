#include <gtest/gtest.h>
#include <CelestialBody.h>
#include <Type.h>

TEST(Helpers, TypeOf)
{
    IO::SDK::Body::CelestialBody cb(301);
    auto res = IO::SDK::Helpers::IsInstanceOf<IO::SDK::Body::CelestialBody>(&cb);
    ASSERT_TRUE(res);

    IO::SDK::Body::Body *body = &cb;
    res = IO::SDK::Helpers::IsInstanceOf<IO::SDK::Body::CelestialBody>(body);
    ASSERT_TRUE(res);
}