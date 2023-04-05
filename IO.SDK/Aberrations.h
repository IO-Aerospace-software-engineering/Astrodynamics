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
		static std::string ToString(AberrationsEnum e) ;

	private:

	};
}
#endif // ! ABERRATION_H


