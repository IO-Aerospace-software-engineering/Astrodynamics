#ifndef SDK_EXCEPTION_H
#define SDK_EXCEPTION_H

#include <exception>
#include <string>

namespace IO::SDK::Exception
{
	class SDKException : public std::exception
	{

	public:
		SDKException(const std::string &msg) : std::exception()
		{
		}

		virtual ~SDKException() = default;
	};
}
#endif // !SDK_EXCEPTION_H
