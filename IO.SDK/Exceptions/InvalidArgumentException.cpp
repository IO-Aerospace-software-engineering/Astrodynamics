/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include<InvalidArgumentException.h>

IO::SDK::Exception::InvalidArgumentException::InvalidArgumentException(const std::string& message):IO::SDK::Exception::SDKException(message)
{

}