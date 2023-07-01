/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <CircularInstrumentKernel.h>

IO::Astrodynamics::Kernels::CircularInstrumentKernel::CircularInstrumentKernel(const IO::Astrodynamics::Instruments::Instrument& instrument, const IO::Astrodynamics::Math::Vector3D& boresight, const IO::Astrodynamics::Math::Vector3D& refVector, const double angle)
	:InstrumentKernel(instrument, boresight, refVector, angle)
{
	BuildKernel();
	furnsh_c(m_filePath.c_str());
	m_isLoaded = true;
}

void IO::Astrodynamics::Kernels::CircularInstrumentKernel::BuildKernel()
{
    InstrumentKernel::BuildKernel();
}
