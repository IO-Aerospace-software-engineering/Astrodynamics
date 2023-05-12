/**
 * @file TooEarlyManeuverException.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<TooEarlyManeuverException.h>

IO::SDK::Exception::TooEarlyManeuverException::TooEarlyManeuverException(const std::string& message):IO::SDK::Exception::SDKException(message)
{

}