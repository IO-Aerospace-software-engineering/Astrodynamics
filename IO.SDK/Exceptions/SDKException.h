/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SDK_EXCEPTION_H
#define SDK_EXCEPTION_H

#include <string>

namespace IO::SDK::Exception
{
	/**
	 * @brief IO SDK Exception 
	 * 
	 */
	class SDKException : public std::exception
	{

	protected:
		const std::string m_msg;

	public:
		explicit SDKException(std::string msg);

		[[nodiscard]] const char *what() const noexcept override;
	};
}
#endif // !SDK_EXCEPTION_H
