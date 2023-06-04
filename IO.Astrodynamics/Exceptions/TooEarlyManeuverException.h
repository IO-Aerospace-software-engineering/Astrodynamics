/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TOO_EARLY_MANEUEVR_EXCEPTION
#define TOO_EARLY_MANEUEVR_EXCEPTION

#include <SDKException.h>
#include <string>

namespace IO::SDK::Exception
{
    /**
     * @brief 
     * 
     */
    class TooEarlyManeuverException final : public SDKException
    {
    private:
        /* data */
    public:
        explicit TooEarlyManeuverException(const std::string &message);
    };

} // namespace IO::SDK::Exception

#endif //TOO_EARLY_MANEUEVR_EXCEPTION