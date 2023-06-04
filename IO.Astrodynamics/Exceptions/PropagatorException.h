/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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