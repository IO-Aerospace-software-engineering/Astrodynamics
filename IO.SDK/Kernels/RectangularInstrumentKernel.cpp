#include "RectangularInstrumentKernel.h"
#include<filesystem>
#include<fstream>

void IO::SDK::Kernels::RectangularInstrumentKernel::BuildKernel()
{
	InstrumentKernel::BuildKernel();

	if (std::filesystem::exists(m_filePath))
	{
		unload_c(m_filePath.c_str());
		std::filesystem::remove(m_filePath);
	}

	std::ofstream outFile(m_filePath);
	std::ifstream readFile(m_templatePath);
	std::string readout;
	std::string search;
	std::string replace;

	if (readFile.good() && outFile.good())
	{
		while (std::getline(readFile, readout))
		{
			auto poscrossangle = readout.find("{cangle}");
			if (poscrossangle != std::string::npos)
			{
				readout = readout.replace(poscrossangle, 8, std::to_string(m_crossAngle));
			}

			outFile << readout << std::endl;
		}

	}
}

IO::SDK::Kernels::RectangularInstrumentKernel::RectangularInstrumentKernel(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& boresight, const IO::SDK::Math::Vector3D& refVector, const double angle, const double crossAngle)
	:InstrumentKernel(instrument, boresight, refVector, angle, "IKRectangualrTemplate.ti"), m_crossAngle{ crossAngle }
{
	BuildKernel();
}
