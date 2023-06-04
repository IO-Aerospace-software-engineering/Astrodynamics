/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <fstream>
#include <sstream>
#include <filesystem>
#include <Templates/Templates.cpp>
#include <Spacecraft.h>

IO::Astrodynamics::Frames::SpacecraftFrameFile::SpacecraftFrameFile(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft) : FrameFile(
        spacecraft.GetFilesPath() + "/Frames/" + spacecraft.GetName() + ".tf", spacecraft.GetName()), m_id{spacecraft.GetId() * 1000}, m_spacecraft{spacecraft}
{
	if (!m_fileExists)
	{
		BuildFrame();
		furnsh_c(m_filePath.c_str());
		m_isLoaded = true;
	}
}

void IO::Astrodynamics::Frames::SpacecraftFrameFile::BuildFrame()
{
	if (std::filesystem::exists(m_filePath))
	{
		unload_c(m_filePath.c_str());
		std::filesystem::remove(m_filePath);
	}

	std::ofstream outFile(m_filePath);
    std::istringstream readTemplate(ckTemplate);
	std::string readout;
	std::string search;
	std::string replace;

	if (readTemplate.good() && outFile.good())
	{
		while (std::getline(readTemplate, readout))
		{
			auto posframeid = readout.find("{frameid}");
			if (posframeid != std::string::npos)
			{
				readout = readout.replace(posframeid, 9, std::to_string(m_id));

				//Check second position
				posframeid = readout.find("{frameid}");
				if (posframeid != std::string::npos)
				{
					readout = readout.replace(posframeid, 9, std::to_string(m_id));
				}
			}

			auto posspname = readout.find("{spacecraftname}");
			if (posspname != std::string::npos)
			{
				readout = readout.replace(posspname, 16, m_spacecraft.GetName());
			}

			auto posframename = readout.find("{framename}");
			if (posframename != std::string::npos)
			{
				readout = readout.replace(posframename, 11, m_name);
			}

			auto posspid = readout.find("{spacecraftid}");
			if (posspid != std::string::npos)
			{
				readout = readout.replace(posspid, 14, std::to_string(m_spacecraft.GetId()));
			}

			outFile << readout << std::endl;
		}
	}

	outFile.flush();
	outFile.close();

//	readTemplate.close();

	m_fileExists = true;
}
