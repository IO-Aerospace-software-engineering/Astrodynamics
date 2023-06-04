/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <SDKException.h>

#include <utility>
IO::Astrodynamics::Exception::SDKException::SDKException(std::string msg) : std::exception(), m_msg{std::move(msg)}
{
}
const char *IO::Astrodynamics::Exception::SDKException::what() const noexcept
{
	return m_msg.c_str();
}