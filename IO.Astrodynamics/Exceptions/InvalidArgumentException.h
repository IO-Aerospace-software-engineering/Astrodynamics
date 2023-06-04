/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef INVALID_ARGUMENT_EXCEPTION
#define INVALID_ARGUMENT_EXCEPTION

#include <SDKException.h>
#include <string>

namespace IO::Astrodynamics::Exception
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

} // namespace IO::Astrodynamics::Exception

#endif //INVALID_ARGUMENT_EXCEPTION