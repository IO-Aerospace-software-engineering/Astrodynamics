//
// Created by sylvain guillet on 3/29/23.
//
#include<gtest/gtest.h>
#include<ScenarioDTO.h>
TEST(API, ScenarioSize)
{
    IO::SDK::API::DTO::ScenarioDTO scenario;
    auto size= sizeof(IO::SDK::API::DTO::ScenarioDTO);
    ASSERT_EQ(2157224,size);
}
