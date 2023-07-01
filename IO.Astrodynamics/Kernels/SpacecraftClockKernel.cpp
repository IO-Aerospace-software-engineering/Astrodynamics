/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <SpacecraftClockKernel.h>
#include <fstream>
#include <sstream>
#include <filesystem>

#include "Templates/Templates.cpp"

using namespace std::chrono_literals;

IO::Astrodynamics::Kernels::SpacecraftClockKernel::SpacecraftClockKernel(const IO::Astrodynamics::Body::Spacecraft::Spacecraft& spacecraft, const int resolution) : Kernel(
        spacecraft.GetFilesPath() + "/Clocks/" + spacecraft.GetName() + ".tsc"), m_spacecraft{spacecraft }, m_resolution{resolution }
{
	if (!m_fileExists)
	{
		BuildGenericClockKernel();
		furnsh_c(m_filePath.c_str());
        m_isLoaded= true;
	}
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> IO::Astrodynamics::Kernels::SpacecraftClockKernel::GetCoverageWindow() const
{
	SpiceDouble pstart[1];
	SpiceDouble pstop[1];

	SpiceInt nparts;

	scpart_c(m_spacecraft.GetId(), &nparts, pstart, pstop);

	double tdbStart;
	double tdbEnd;

	sct2e_c(m_spacecraft.GetId(), pstart[0], &tdbStart);
	sct2e_c(m_spacecraft.GetId(), pstop[0], &tdbEnd);

	return IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>{IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(tdbStart)), IO::Astrodynamics::Time::TDB(std::chrono::duration<double>(tdbEnd))};
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::Kernels::SpacecraftClockKernel::ConvertToTDB(const std::string& clock) const
{
	double et;
	scs2e_c(m_spacecraft.GetId(), clock.c_str(), &et);
	return IO::Astrodynamics::Time::TDB{std::chrono::duration<double>(et)};
}

IO::Astrodynamics::Time::TDB IO::Astrodynamics::Kernels::SpacecraftClockKernel::ConvertToTDB(const double encodedClock) const
{
	double et{};
	sct2e_c(m_spacecraft.GetId(), encodedClock, &et);
	return IO::Astrodynamics::Time::TDB{std::chrono::duration<double>(et)};
}

std::string IO::Astrodynamics::Kernels::SpacecraftClockKernel::ConvertToClockString(const IO::Astrodynamics::Time::TDB& epoch) const
{
	SpiceChar sclk[30];
	sce2s_c(m_spacecraft.GetId(), epoch.GetSecondsFromJ2000().count(), 30, sclk);
	return sclk;
}

void IO::Astrodynamics::Kernels::SpacecraftClockKernel::BuildGenericClockKernel()
{
	if (std::filesystem::exists(m_filePath))
	{
		unload_c(m_filePath.c_str());
		std::filesystem::remove(m_filePath);
	}

	std::ofstream outFile(m_filePath, std::ios::out);
	std::stringstream readTemplate(Sclk);
	std::string readout;
	std::string search;
	std::string replace;

	std::string positiveId{ std::to_string(std::abs(m_spacecraft.GetId())) };

	if (readTemplate.good() && outFile.good())
	{
		while (std::getline(readTemplate, readout))
		{
			auto pos = readout.find("{id}");
			if (pos != std::string::npos)
			{
				readout = readout.replace(pos, 4, positiveId);
			}

			pos = readout.find("{resolution}");
			if (pos != std::string::npos)
			{
				readout = readout.replace(pos, 12, std::to_string(GetTicksPerSeconds()));
			}

			outFile << readout << std::endl;
		}
	}

	outFile.flush();
	outFile.close();

    m_fileExists= true;
}

double IO::Astrodynamics::Kernels::SpacecraftClockKernel::ConvertToEncodedClock(const IO::Astrodynamics::Time::TDB& tdb) const
{
    return ConvertToEncodedClock(m_spacecraft.GetId(),tdb);
}

int IO::Astrodynamics::Kernels::SpacecraftClockKernel::GetTicksPerSeconds() const
{
	return  std::pow(2, m_resolution);
}

double IO::Astrodynamics::Kernels::SpacecraftClockKernel::GetSecondsPerTick() const
{
	return 1.0 / GetTicksPerSeconds();
}

double IO::Astrodynamics::Kernels::SpacecraftClockKernel::ConvertToEncodedClock(int spacecraftId, const IO::Astrodynamics::Time::TDB &epoch)
{
    double enc{};
    sce2c_c(spacecraftId, epoch.GetSecondsFromJ2000().count(), &enc);
    return enc;
}
