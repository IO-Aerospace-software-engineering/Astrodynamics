/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include<PropagatorException.h>

IO::Astrodynamics::Exception::PropagatorException::PropagatorException(const std::string& message):IO::Astrodynamics::Exception::SDKException(message)
{

}