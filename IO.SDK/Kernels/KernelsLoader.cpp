/**
 * @file KernelsLoader.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <KernelsLoader.h>
#include<filesystem>
#include<SpiceUsr.h>
#include <SDKException.h>

void IO::SDK::Kernels::KernelsLoader::Load(const std::string &path)
{
    if (std::filesystem::is_regular_file(path))
    {
        if (!std::filesystem::exists(path))
        {
            throw IO::SDK::Exception::SDKException("Impossible to load kernel. The file doesn't exist :" + path);
        }

        unload_c(path.c_str());
        furnsh_c(path.c_str());
    } else if (std::filesystem::is_directory(path))
    {
        if (!std::filesystem::exists(path))
        {
            throw IO::SDK::Exception::SDKException("Impossible to load kernels. The directory doesn't exist :" + path);
        }

        for (const auto &entry: std::filesystem::directory_iterator(path))
        {
            Load(entry.path().string());
        }
    }

}
