/**
 * @file TooEarlyManeuverException.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
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