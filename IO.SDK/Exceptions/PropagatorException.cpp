/**
 * @file PropagatorException.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<PropagatorException.h>

IO::SDK::Exception::PropagatorException::PropagatorException(const std::string& message):IO::SDK::Exception::SDKException(message)
{

}