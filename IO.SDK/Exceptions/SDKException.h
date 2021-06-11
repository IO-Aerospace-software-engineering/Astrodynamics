/**
 * @file SDKException.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SDK_EXCEPTION_H
#define SDK_EXCEPTION_H

#include <exception>
#include <string>

namespace IO::SDK::Exception
{
	class SDKException : public std::exception
	{

	protected:
		const std::string m_msg;

	public:
		SDKException(const std::string &msg);

		virtual ~SDKException() = default;

		const char *what() const noexcept override;
	};
}
#endif // !SDK_EXCEPTION_H
