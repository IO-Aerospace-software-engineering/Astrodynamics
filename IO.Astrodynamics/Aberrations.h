/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef  ABERRATION_H
#define ABERRATION_H

#include <string>
#include <map>

namespace IO::Astrodynamics
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


