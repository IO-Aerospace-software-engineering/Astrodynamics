/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef SOLAR_SYSTEM_KERNELS_H
#define SOLAR_SYSTEM_KERNELS_H

#include <string>

namespace IO::Astrodynamics::Kernels
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
        static void Unload(const std::string& path);
	};
}

#endif // !SOLAR_SYSTEM_KERNELS_H
