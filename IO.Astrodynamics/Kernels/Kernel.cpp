/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Kernel.h>
#include <filesystem>
#include <cstring>

constexpr size_t COMLENGTH = 80;

IO::Astrodynamics::Kernels::Kernel::Kernel(std::string filePath) : m_filePath{std::move(filePath)}
{

    auto directory = std::filesystem::directory_entry(m_filePath).path();
    if (directory.has_parent_path())
    {
        if (!std::filesystem::exists(directory.parent_path()))
        {
            std::filesystem::create_directories(directory.parent_path());
        }
    }
}

IO::Astrodynamics::Kernels::Kernel::~Kernel()
{
    unload_c(m_filePath.c_str());
}

std::string IO::Astrodynamics::Kernels::Kernel::GetPath() const
{
    return m_filePath;
}

bool IO::Astrodynamics::Kernels::Kernel::IsLoaded() const
{
    return m_isLoaded;
}

void IO::Astrodynamics::Kernels::Kernel::AddComment(const std::string &comment) const
{
    if (comment.size() >= COMLENGTH)
    {
        throw IO::Astrodynamics::Exception::SDKException("Comment size must be lower than " + std::to_string(COMLENGTH) + " chars");
    }

    SpiceInt handle;
    SpiceChar buffer[1][COMLENGTH + 1];
    std::strcpy(buffer[0], comment.c_str());

    //Unbload kernel
    unload_c(m_filePath.c_str());

    //write comment
    dafopw_c(m_filePath.c_str(), &handle);
    dafac_c(handle, 1, COMLENGTH + 1, buffer);
    dafcls_c(handle);

    //reload kernel
    furnsh_c(m_filePath.c_str());
}

std::string IO::Astrodynamics::Kernels::Kernel::ReadComment() const
{
    SpiceInt handle;
    SpiceInt n;
    SpiceChar buffer[1][COMLENGTH + 1];
    SpiceBoolean done = SPICEFALSE;

    dafopr_c(m_filePath.c_str(), &handle);
    dafec_c(handle, 1, COMLENGTH + 1, &n, buffer, &done);
    dafcls_c(handle);

    return std::string{buffer[0]};
}

int IO::Astrodynamics::Kernels::Kernel::DefinePolynomialDegree(const int dataSize, const int maximumDegree)
{
    //min size used to define polynomial degree
    int degree{dataSize - 1};

    if (degree < 1)
    {
        throw IO::Astrodynamics::Exception::SDKException("Insuffisant data provided. 2 data are required at least");
    }

    if (degree > maximumDegree)
    {
        degree = maximumDegree;
    } else if (degree % 2 == 0)
    {
        degree = degree - 1;
    }

    return degree;
}


