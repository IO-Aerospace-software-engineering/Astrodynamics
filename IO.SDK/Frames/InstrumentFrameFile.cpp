/**
 * @file InstrumentFrameFile.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <InstrumentFrameFile.h>
#include<filesystem>
#include<fstream>
#include<sstream>
#include<Templates/Templates.cpp>

IO::SDK::Frames::InstrumentFrameFile::InstrumentFrameFile(const IO::SDK::Instruments::Instrument& instrument, const IO::SDK::Math::Vector3D& orientation) :FrameFile(instrument.GetFilesPath() + "/Frames/" + instrument.GetName() + ".tf", instrument.GetSpacecraft().GetName() + "_" + instrument.GetName()), m_instrument{ instrument }, m_orientation{ orientation }
{
	if (!m_fileExists)
	{
		BuildFrame();
		furnsh_c(m_filePath.c_str());
		m_isLoaded = true;
	}
}

void IO::SDK::Frames::InstrumentFrameFile::BuildFrame()
{
	if (std::filesystem::exists(m_filePath))
	{
		unload_c(m_filePath.c_str());
		std::filesystem::remove(m_filePath);
	}

	std::ofstream outFile(m_filePath);
	std::stringstream readTemplate(Tk);
	std::string readout;
	std::string search;
	std::string replace;

	if (readTemplate.good() && outFile.good())
	{
		while (std::getline(readTemplate, readout))
		{
			auto posspname = readout.find("{spacecraftname}");
			if (posspname != std::string::npos)
			{
				readout = readout.replace(posspname, 16, m_instrument.GetSpacecraft().GetName());
			}

			auto posinstname = readout.find("{instrumentname}");
			if (posinstname != std::string::npos)
			{
				readout = readout.replace(posinstname, 16, m_instrument.GetName());
			}

			auto posframename = readout.find("{framename}");
			if (posframename != std::string::npos)
			{
				readout = readout.replace(posframename, 11, m_name);
			}

			auto posinstid = readout.find("{instrumentid}");
			if (posinstid != std::string::npos)
			{
				readout = readout.replace(posinstid, 14, std::to_string(m_instrument.GetId()));

				posinstid = readout.find("{instrumentid}");
				if (posinstid != std::string::npos)
				{
					readout = readout.replace(posinstid, 14, std::to_string(m_instrument.GetId()));
				}
			}

			auto posspid = readout.find("{spacecraftid}");
			if (posspid != std::string::npos)
			{
				readout = readout.replace(posspid, 14, std::to_string(m_instrument.GetSpacecraft().GetId()));
			}

			auto posx = readout.find("{x}");
			if (posx != std::string::npos)
			{
				readout = readout.replace(posx, 3, std::to_string(m_orientation.GetX() * -1));
			}

			auto posy = readout.find("{y}");
			if (posy != std::string::npos)
			{
				readout = readout.replace(posy, 3, std::to_string(m_orientation.GetY() * -1));
			}

			auto posz = readout.find("{z}");
			if (posz != std::string::npos)
			{
				readout = readout.replace(posz, 3, std::to_string(m_orientation.GetZ() * -1));
			}


			outFile << readout << std::endl;
		}
	}

	outFile.flush();
	outFile.close();

	m_fileExists = true;
}