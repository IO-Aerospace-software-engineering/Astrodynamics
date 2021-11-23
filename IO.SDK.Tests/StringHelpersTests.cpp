#include <gtest/gtest.h>
#include <string>
#include <StringHelpers.h>

TEST(StringHelpers, YoUppercase)
{
	std::string test = "test";
	std::string upperTest = IO::SDK::StringHelpers::ToUpper(test);
	ASSERT_STREQ("TEST", upperTest.c_str());
	ASSERT_STREQ("test", test.c_str());
}