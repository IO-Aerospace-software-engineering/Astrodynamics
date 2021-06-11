#include<InvalidArgumentException.h>

IO::SDK::Exception::InvalidArgumentException::InvalidArgumentException(const std::string& message):IO::SDK::Exception::SDKException(message)
{

}