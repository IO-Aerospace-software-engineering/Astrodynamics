#ifndef  ABERRATION_H
#define ABERRATION_H
#include<string>

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
	public:
		std::string ToString(const AberrationsEnum e) const;

	private:

	};
}
#endif // ! ABERRATION_H


