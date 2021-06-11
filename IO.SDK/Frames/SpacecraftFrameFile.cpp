#include <SpacecraftFrameFile.h>
#include<Parameters.h>
#include<SpiceUsr.h>
#include<iostream>
#include<fstream>
#include<filesystem>

IO::SDK::Frames::SpacecraftFrameFile::SpacecraftFrameFile(const IO::SDK::Body::Spacecraft::Spacecraft& spacecraft) :FrameFile(spacecraft.GetFilesPath() + "/Frames/" + spacecraft.GetName() + ".tf", spacecraft.GetName()), m_id{ spacecraft.GetId() * 1000 }, m_spacecraft{ spacecraft }
{
	if (!m_fileExists)
	{
		BuildFrame();
		furnsh_c(m_filePath.c_str());
		m_isLoaded = true;
	}
}

void IO::SDK::Frames::SpacecraftFrameFile::BuildFrame()
{
	if (std::filesystem::exists(m_filePath))
	{
		unload_c(m_filePath.c_str());
		std::filesystem::remove(m_filePath);
	}

	std::ofstream outFile(m_filePath);
	std::ifstream readFile(std::string(IO::SDK::Parameters::KernelTemplates) + "/cktemplate.tf");
	std::string readout;
	std::string search;
	std::string replace;

	if (readFile.good() && outFile.good())
	{
		while (std::getline(readFile, readout))
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

	readFile.close();

	m_fileExists = true;
}
