/**
 * @file Type.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
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