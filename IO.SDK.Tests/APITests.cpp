#include<gtest/gtest.h>
#include<ScenarioDTO.h>
TEST(API, DTOSize)
{
    auto size= sizeof(IO::SDK::API::DTO::ScenarioDTO);
    ASSERT_EQ(2160664,size);
}
