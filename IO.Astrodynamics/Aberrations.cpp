/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Aberrations.h>
#include <SDKException.h>

std::string IO::SDK::Aberrations::ToString(const AberrationsEnum e)
{
    auto it = IO::SDK::Aberrations::AberrationStrings.find(e);
    return it == IO::SDK::Aberrations::AberrationStrings.end() ? std::string("Out of range") : std::string(it->second);
}

IO::SDK::AberrationsEnum IO::SDK::Aberrations::ToEnum(const std::string& e)
{
    for (auto& abe: AberrationStrings)
    {
        if (abe.second == e)
        {
            return abe.first;
        }
    }

    throw IO::SDK::Exception::SDKException("Invalid aberration name : " + e);
}
