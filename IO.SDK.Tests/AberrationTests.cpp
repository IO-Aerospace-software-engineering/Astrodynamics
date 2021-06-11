#include<gtest/gtest.h>
#include<Aberrations.h>
TEST(Aberration, ToString)
{
	IO::SDK::Aberrations a{};
	ASSERT_STREQ("XCN+S", a.ToString(IO::SDK::AberrationsEnum::XCNS).c_str());
}