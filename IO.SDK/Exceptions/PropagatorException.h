/**
 * @file PropagatorException.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef PROPAGATOR_EXCEPTION
#define PROPAGATOR_EXCEPTION

#include <SDKException.h>

namespace IO::SDK::Exception
{
    /**
     * @brief Propagator exception
     * 
     */
    class PropagatorException final : public SDKException
    {
    private:
        /* data */
    public:
        explicit PropagatorException(const std::string &message);
    };

} // namespace IO::SDK::Exception

#endif //PROPAGATOR_EXCEPTION