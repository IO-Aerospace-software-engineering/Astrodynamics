/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef STRING_HELPERS_H
#define STRING_HELPERS_H
#include <string>
#include <algorithm>

namespace IO::Astrodynamics
{
    class StringHelpers
    {
    private:
    public:
        static std::string ToUpper(std::string input)
        {
            std::transform(input.begin(), input.end(), input.begin(), ::toupper);

            return input;
        }
    };
}
#endif