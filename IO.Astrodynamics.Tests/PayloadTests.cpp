#include <gtest/gtest.h>
#include <Payload.h>
#include <InvalidArgumentException.h>

TEST(Payload, Initialization)
{
	IO::Astrodynamics::Body::Spacecraft::Payload payload("sn1", "payload1", 123.5);
	ASSERT_STREQ("payload1", payload.GetName().c_str());
	ASSERT_STREQ("sn1", payload.GetSerialNumber().c_str());
	ASSERT_DOUBLE_EQ(123.5, payload.GetMass());
}

TEST(Payload, InvalidName)
{
	ASSERT_THROW(IO::Astrodynamics::Body::Spacecraft::Payload payload("sn1", "", 123.5), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Payload, InvalidSerialNumber)
{
	ASSERT_THROW(IO::Astrodynamics::Body::Spacecraft::Payload payload("", "payload1", 123.5), IO::Astrodynamics::Exception::InvalidArgumentException);
}

TEST(Payload, InvalidMass)
{
	ASSERT_THROW(IO::Astrodynamics::Body::Spacecraft::Payload payload("sn1", "payload1", 0.0), IO::Astrodynamics::Exception::InvalidArgumentException);
}