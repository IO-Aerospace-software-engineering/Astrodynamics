#ifndef FOV_SHAPES_H
#define FOV_SHAPES_H

#include<string>

namespace IO::SDK::Instruments
{
	enum class FOVShapeEnum
	{
		Circular,
		Elliptical,
		Rectangular
	};

	class FOVShapes final
	{
	public:
	
		std::string ToString(const FOVShapeEnum e) const;
	};
}

#endif
