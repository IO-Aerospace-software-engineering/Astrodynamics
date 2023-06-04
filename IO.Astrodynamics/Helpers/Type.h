/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef TYPE_CPP
#define TYPE_CPP

namespace IO::SDK::Helpers
{
    template <typename T, typename Base>
    inline bool IsInstanceOf(const Base * src)
    {
        return dynamic_cast<const T*>(src) != nullptr;
    }
}

#endif