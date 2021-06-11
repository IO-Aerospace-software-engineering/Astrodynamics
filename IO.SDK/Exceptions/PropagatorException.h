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