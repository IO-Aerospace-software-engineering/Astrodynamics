/**
 * @file KernelsLoader.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SOLAR_SYSTEM_KERNELS_H
#define SOLAR_SYSTEM_KERNELS_H

#include <string>

namespace IO::SDK::Kernels
{
	/**
	 * @brief 
	 * 
	 */
	class KernelsLoader final
	{
    private:
    public :
        static void Load(const std::string& path);
	};
}

#endif // !SOLAR_SYSTEM_KERNELS_H