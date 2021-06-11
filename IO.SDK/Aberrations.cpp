#include "Aberrations.h"
#include<map>

std::string IO::SDK::Aberrations::ToString(const AberrationsEnum e) const
{
	const std::map<AberrationsEnum, const char*> AberrationStrings{
		{ AberrationsEnum::None, "NONE" },
		{ AberrationsEnum::LT, "LT" },
		{ AberrationsEnum::LTS, "LT+S" },
		{ AberrationsEnum::CN, "CN" },
		{ AberrationsEnum::CNS, "CN+S" },
		{ AberrationsEnum::XLT, "XLT" },
		{ AberrationsEnum::XLTS, "XLT+S" },
		{ AberrationsEnum::XCN, "XCN" },
		{ AberrationsEnum::XCNS, "XCN+S" }
	};
	auto   it = AberrationStrings.find(e);
	return it == AberrationStrings.end() ? std::string("Out of range") : std::string(it->second);
}