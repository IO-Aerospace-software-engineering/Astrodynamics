/**
 * @file Aberrations.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef  ABERRATION_H
#define ABERRATION_H

#include <string>
#include <map>

namespace IO::SDK
{
    enum class AberrationsEnum
    {
        None,
        LT,
        LTS,
        CN,
        CNS,
        XLT,
        XLTS,
        XCN,
        XCNS
    };

    class Aberrations
    {
    private:
        static inline std::map<AberrationsEnum, const char *> AberrationStrings{
                {AberrationsEnum::None, "NONE"},
                {AberrationsEnum::LT,   "LT"},
                {AberrationsEnum::LTS,  "LT+S"},
                {AberrationsEnum::CN,   "CN"},
                {AberrationsEnum::CNS,  "CN+S"},
                {AberrationsEnum::XLT,  "XLT"},
                {AberrationsEnum::XLTS, "XLT+S"},
                {AberrationsEnum::XCN,  "XCN"},
                {AberrationsEnum::XCNS, "XCN+S"}
        };
    public:
        static std::string ToString(AberrationsEnum e);

        static AberrationsEnum ToEnum(const std::string &e);
    };
}
#endif // ! ABERRATION_H


