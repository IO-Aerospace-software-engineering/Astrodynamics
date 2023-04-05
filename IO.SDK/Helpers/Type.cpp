/**
 * @file Type.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
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