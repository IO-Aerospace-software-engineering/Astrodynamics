#include <Kernel.h>
#include <Parameters.h>
#include <SpiceUsr.h>
#include <SDKException.h>
#include<filesystem>
#include<cstring>

constexpr size_t COMLENGTH = 80;

IO::SDK::Kernels::Kernel::Kernel(const std::string& filePath) : m_filePath{ filePath }
{

	auto directory = std::filesystem::directory_entry(filePath).path();
	if (directory.has_parent_path())
	{
		if (!std::filesystem::exists(directory.parent_path()))
		{
			std::filesystem::create_directories(directory.parent_path());
		}
	}

	if (std::filesystem::is_block_file(m_filePath) && std::filesystem::exists(m_filePath))
	{
		m_fileExists = true;
		furnsh_c(m_filePath.c_str());
		m_isLoaded = true;
	}

}

IO::SDK::Kernels::Kernel::~Kernel()
{
	unload_c(m_filePath.c_str());
}

std::string IO::SDK::Kernels::Kernel::GetPath() const
{
	return m_filePath;
}

bool IO::SDK::Kernels::Kernel::IsLoaded() const
{
	return m_isLoaded;
}

void IO::SDK::Kernels::Kernel::AddComment(const std::string& comment) const
{
	if (comment.size() >= COMLENGTH)
	{
		throw IO::SDK::Exception::SDKException("Comment size must be lower than " + std::to_string(COMLENGTH) + " chars");
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

std::string IO::SDK::Kernels::Kernel::ReadComment() const
{
	SpiceInt handle;
	SpiceInt n;
	SpiceChar buffer[1][COMLENGTH + 1];
	SpiceBoolean done = SPICEFALSE;

	dafopr_c(m_filePath.c_str(), &handle);
	dafec_c(handle, 1, COMLENGTH + 1, &n, buffer, &done);
	dafcls_c(handle);

	return std::string(buffer[0]);
}

int IO::SDK::Kernels::Kernel::DefinePolynomialDegree(const int dataSize, const int maximumDegree) const
{
	//min size used to define polynomial degree
	int degree{ dataSize };

	if (degree < 2)
	{
		throw IO::SDK::Exception::SDKException("Insuffisant data provided. 2 data are required at least");
	}

	if (degree > maximumDegree)
	{
		degree = maximumDegree;
	}
	else if (degree % 2 == 0)
	{
		degree = degree - 1;
	}

	return degree;
}


