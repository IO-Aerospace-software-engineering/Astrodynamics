/**
 * @file PropagatorException.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef PROPAGATOR_EXCEPTION
#define PROPAGATOR_EXCEPTION

#include <SDKException.h>
#include <string>

namespace IO::SDK::Exception
{
    class PropagatorException : public SDKException
    {
    private:
        /* data */
    public:
        PropagatorException(const std::string &message);
        ~PropagatorException() = default;
    };

} // namespace IO::SDK::Exception

#endif //PROPAGATOR_EXCEPTION