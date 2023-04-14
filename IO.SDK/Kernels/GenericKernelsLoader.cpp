/**
 * @file GenericKernelsLoader.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "GenericKernelsLoader.h"
#include<filesystem>
#include<Parameters.h>
#include<SpiceUsr.h>
#include <SDKException.h>

void IO::SDK::Kernels::GenericKernelsLoader::Load(const std::string &directoryPath)
{
    if (!std::filesystem::exists(directoryPath))
    {
        throw IO::SDK::Exception::SDKException("Impossible to load generic kernels. The directory doesn't exist :" + directoryPath);
    }

    for (const auto &entry: std::filesystem::directory_iterator(directoryPath))
    {
        furnsh_c(entry.path().string().c_str());
    }
}
