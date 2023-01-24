/**
 * @file CircularInstrumentKernel.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "CircularInstrumentKernel.h"
#include<filesystem>
#include<fstream>

IO::SDK::Kernels::CircularInstrumentKernel::CircularInstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, const double angle)
	:InstrumentKernel(instrument, boresight, refVector, angle)
{
	BuildKernel();
	furnsh_c(m_filePath.c_str());
	m_isLoaded = true;
}
