/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Aberrations.h>
#include <SDKException.h>

std::string IO::Astrodynamics::Aberrations::ToString(const AberrationsEnum e)
{
    auto it = IO::Astrodynamics::Aberrations::AberrationStrings.find(e);
    return it == IO::Astrodynamics::Aberrations::AberrationStrings.end() ? std::string("Out of range") : std::string(it->second);
}

IO::Astrodynamics::AberrationsEnum IO::Astrodynamics::Aberrations::ToEnum(const std::string& e)
{
    for (auto& abe: AberrationStrings)
    {
        if (abe.second == e)
        {
            return abe.first;
        }
    }

    throw IO::Astrodynamics::Exception::SDKException("Invalid aberration name : " + e);
}
