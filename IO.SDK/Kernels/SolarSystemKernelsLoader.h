/**
 * @file SolarSystemKernelsLoader.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-02
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef SOLAR_SYSTEM_KERNELS_H
#define SOLAR_SYSTEM_KERNELS_H

namespace IO::SDK::Kernels
{
	/**
	 * @brief 
	 * 
	 */
	class SolarSystemKernelsLoader final
	{
	private:
		static SolarSystemKernelsLoader m_instance;

		/**
		 * @brief Construct a new Solar System Kernels Loader object
		 * 
		 */
		SolarSystemKernelsLoader();
	};
}

#endif // !SOLAR_SYSTEM_KERNELS_H
