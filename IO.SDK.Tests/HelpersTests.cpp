#include <gtest/gtest.h>
#include <CelestialBody.h>
#include "../IO.SDK/Helpers/Type.cpp"

TEST(Helpers, TypeOf)
{
    IO::SDK::Body::CelestialBody cb(301, "moon");
    auto res = IO::SDK::Helpers::IsInstanceOf<IO::SDK::Body::CelestialBody>(&cb);
    ASSERT_TRUE(res);

    IO::SDK::Body::Body *body = &cb;
    res = IO::SDK::Helpers::IsInstanceOf<IO::SDK::Body::CelestialBody>(body);
    ASSERT_TRUE(res);
}