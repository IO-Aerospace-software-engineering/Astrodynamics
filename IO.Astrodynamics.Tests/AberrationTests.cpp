#include<gtest/gtest.h>
#include<Aberrations.h>
TEST(Aberration, ToString)
{
	IO::Astrodynamics::Aberrations a{};
	ASSERT_STREQ("XCN+S", a.ToString(IO::Astrodynamics::AberrationsEnum::XCNS).c_str());
}