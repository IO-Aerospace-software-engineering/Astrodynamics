/**
 * @file FOVShapes.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "FOVShapes.h"
#include<map>

std::string IO::SDK::Instruments::FOVShapes::ToString(const FOVShapeEnum e)
{
	const std::map<IO::SDK::Instruments::FOVShapeEnum, const char*> FOVStrings{
		{ FOVShapeEnum::Circular, "CIRCLE" },
		{ FOVShapeEnum::Elliptical, "ELLIPSE" },
		{ FOVShapeEnum::Rectangular, "RECTANGLE" }
	};
	auto   it = FOVStrings.find(e);
	return it == FOVStrings.end() ? std::string("Out of range") : std::string(it->second);
}