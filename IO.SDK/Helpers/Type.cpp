#ifndef TYPE_CPP
#define TYPE_CPP

#include <iostream>

namespace IO::SDK::Helpers
{
    template <typename T, typename Base>
    inline bool IsInstanceOf(const Base * src)
    {
        return dynamic_cast<const T*>(src) != nullptr;
    }
}

#endif