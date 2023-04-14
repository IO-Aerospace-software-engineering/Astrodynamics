#include<gtest/gtest.h>
#include<GenericKernelsLoader.h>

int main(int argc, char** argv)
{
	::testing::InitGoogleTest(&argc, argv);

    IO::SDK::Kernels::GenericKernelsLoader::Load("Data/SolarSystem");
	
	//IO::SDK::Kernels::KernelsManager::GetInstance().LoadKernel("Data/naif0012.tls.pc");
	return RUN_ALL_TESTS();
}