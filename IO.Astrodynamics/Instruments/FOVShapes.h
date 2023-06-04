/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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
		/**
		 * @brief Get enum string value
		 * 
		 * @param e 
		 * @return std::string 
		 */
		static std::string ToString(FOVShapeEnum e) ;
	};
}

#endif
