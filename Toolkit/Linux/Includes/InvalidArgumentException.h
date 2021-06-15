/**
 * @file InvalidArgumentException.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef INVALID_ARGUMENT_EXCEPTION
#define INVALID_ARGUMENT_EXCEPTION

#include <SDKException.h>
#include <string>

namespace IO::SDK::Exception
{
    class InvalidArgumentException : public SDKException
    {
    private:
        /* data */
    public:
        InvalidArgumentException(const std::string &message);
        ~InvalidArgumentException() = default;
    };

} // namespace IO::SDK::Exception

#endif //INVALID_ARGUMENT_EXCETION