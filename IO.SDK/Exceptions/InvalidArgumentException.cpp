/**
 * @file InvalidArgumentException.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<InvalidArgumentException.h>

IO::SDK::Exception::InvalidArgumentException::InvalidArgumentException(const std::string& message):IO::SDK::Exception::SDKException(message)
{

}