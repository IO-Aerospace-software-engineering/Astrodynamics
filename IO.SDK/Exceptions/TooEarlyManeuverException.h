#ifndef TOO_EARLY_MANEUEVR_EXCEPTION
#define TOO_EARLY_MANEUEVR_EXCEPTION

#include <SDKException.h>
#include <string>

namespace IO::SDK::Exception
{
    class TooEarlyManeuverException : public SDKException
    {
    private:
        /* data */
    public:
        TooEarlyManeuverException(const std::string &message);
        ~TooEarlyManeuverException() = default;
    };

} // namespace IO::SDK::Exception

#endif //TOO_EARLY_MANEUEVR_EXCEPTION