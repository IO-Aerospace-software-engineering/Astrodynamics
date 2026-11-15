/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <KernelsLoader.h>
#include<filesystem>
#include<SpiceUsr.h>
#include <SDKException.h>

void IO::Astrodynamics::Kernels::KernelsLoader::Load(const std::string &path)
{
    if (std::filesystem::is_regular_file(path))
    {
        if (!std::filesystem::exists(path))
        {
            throw IO::Astrodynamics::Exception::SDKException("Impossible to load kernel. The file doesn't exist :" + path);
        }

        unload_c(path.c_str());
        furnsh_c(path.c_str());
    } else if (std::filesystem::is_directory(path))
    {
        if (!std::filesystem::exists(path))
        {
            throw IO::Astrodynamics::Exception::SDKException("Impossible to load kernels. The directory doesn't exist :" + path);
        }

        for (const auto &entry: std::filesystem::directory_iterator(path))
        {
            Load(entry.path().string());
        }
    }

}

void IO::Astrodynamics::Kernels::KernelsLoader::Unload(const std::string &path)
{
    // A directory is walked as it is now; any other path is handed to SPICE by its name, whether it still exists or not:
    // unload_c unloads a file by the name it was loaded under, and does nothing for a name it does not hold
    std::error_code error;
    if (std::filesystem::is_directory(path, error))
    {
        for (const auto &entry: std::filesystem::directory_iterator(path))
        {
            Unload(entry.path().string());
        }
    } else
    {
        unload_c(path.c_str());
    }
}
