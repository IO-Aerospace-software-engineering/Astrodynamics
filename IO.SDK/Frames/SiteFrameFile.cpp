/**
 * @file SiteFrameFile.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <SiteFrameFile.h>
#include <filesystem>
#include <fstream>
#include <string>
#include <Parameters.h>
#include <Site.h>

IO::SDK::Frames::SiteFrameFile::SiteFrameFile(const IO::SDK::Sites::Site &site) : FrameFile(std::string(IO::SDK::Parameters::SiteFramesPath) + "/" + site.GetName() + ".tf", site.GetName() + "_TOPO"), m_site{site}
{
    if (!m_fileExists)
    {
        BuildFrame();
        furnsh_c(m_filePath.c_str());
        m_isLoaded = true;
    }
}

void IO::SDK::Frames::SiteFrameFile::BuildFrame()
{
    if (std::filesystem::exists(m_filePath))
    {
        unload_c(m_filePath.c_str());
        std::filesystem::remove(m_filePath);
    }

    std::ofstream outFile(m_filePath);
    std::ifstream readFile(std::string(IO::SDK::Parameters::KernelTemplates) + "/sitetktemplate.tf");
    std::string readout;
    std::string search;
    std::string replace;

    if (readFile.good() && outFile.good())
    {
        while (std::getline(readFile, readout))
        {
            auto posspname = readout.find("{sitename}");
            if (posspname != std::string::npos)
            {
                readout = readout.replace(posspname, 10, m_site.GetName());
            }

            auto possitenametopo = readout.find("{sitenametopo}");
            if (possitenametopo != std::string::npos)
            {
                readout = readout.replace(possitenametopo, 14, m_name);
            }

            auto posfrid = readout.find("{frameid}");
            if (posfrid != std::string::npos)
            {
                readout = readout.replace(posfrid, 9, std::to_string(1000000 + m_site.GetBody()->GetId() * 1000 + m_site.GetId()));

                posfrid = readout.find("{frameid}");
                if (posfrid != std::string::npos)
                {
                    readout = readout.replace(posfrid, 9, std::to_string(1000000 + m_site.GetBody()->GetId() * 1000 + m_site.GetId()));
                }
            }

            auto possiteid = readout.find("{siteid}");
            if (possiteid != std::string::npos)
            {
                readout = readout.replace(possiteid, 8, std::to_string(m_site.GetBody()->GetId() * 1000 + m_site.GetId()));
            }

            auto posframename = readout.find("{fixedframe}");
            if (posframename != std::string::npos)
            {
                readout = readout.replace(posframename, 12, m_site.GetBody()->GetBodyFixedFrame().GetName());
            }

            auto poslong = readout.find("{long}");
            if (poslong != std::string::npos)
            {
                readout = readout.replace(poslong, 6, std::to_string(-m_site.GetCoordinates().GetLongitude()));
            }

            auto poscolat = readout.find("{colat}");
            if (poscolat != std::string::npos)
            {
                readout = readout.replace(poscolat, 7, std::to_string(-(IO::SDK::Constants::PI2 - m_site.GetCoordinates().GetLatitude())));
            }

            outFile << readout << std::endl;
        }
    }

    outFile.flush();
    outFile.close();

    readFile.close();

    m_fileExists = true;
}