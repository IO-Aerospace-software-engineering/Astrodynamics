/**
 * @file FOVShapes.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef FOV_SHAPES_H
#define FOV_SHAPES_H

#include <string>

namespace IO::SDK::Instruments
{
	enum class FOVShapeEnum
	{
		Circular,
		Elliptical,
		Rectangular
	};

	/**
	 * @brief Field of view shape
	 * 
	 */
	class FOVShapes final
	{
	public:
		std::string ToString(const FOVShapeEnum e) const;
	};
}

#endif
