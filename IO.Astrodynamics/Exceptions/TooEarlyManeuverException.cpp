/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include<TooEarlyManeuverException.h>

IO::Astrodynamics::Exception::TooEarlyManeuverException::TooEarlyManeuverException(const std::string& message):IO::Astrodynamics::Exception::SDKException(message)
{

}