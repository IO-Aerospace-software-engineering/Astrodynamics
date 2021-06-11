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