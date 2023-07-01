/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <FOVShapes.h>
#include <map>

std::string IO::Astrodynamics::Instruments::FOVShapes::ToString(const FOVShapeEnum e)
{
    const std::map<IO::Astrodynamics::Instruments::FOVShapeEnum, const char *> FOVStrings{
            {FOVShapeEnum::Circular,    "CIRCLE"},
            {FOVShapeEnum::Elliptical,  "ELLIPSE"},
            {FOVShapeEnum::Rectangular, "RECTANGLE"}
    };
    auto it = FOVStrings.find(e);
    return it == FOVStrings.end() ? std::string("Out of range") : std::string(it->second);
}