/**
 * @file InvalidArgumentException.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
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
    /**
     * @brief Invalid argument exception
     * 
     */
    class InvalidArgumentException final: public SDKException
    {
    private:
        /* data */
    public:
        explicit InvalidArgumentException(const std::string &message);
    };

} // namespace IO::SDK::Exception

#endif //INVALID_ARGUMENT_EXCEPTION