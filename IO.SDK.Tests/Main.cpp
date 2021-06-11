#include<gtest/gtest.h>
#include<Constants.h>

int main(int argc, char** argv)
{
	::testing::InitGoogleTest(&argc, argv);

	
	//IO::SDK::Kernels::KernelsManager::GetInstance().LoadKernel("Data/naif0012.tls.pc");
	return RUN_ALL_TESTS();
}