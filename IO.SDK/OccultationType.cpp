/**
 * @file OccultationType.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<OccultationType.h>

IO::SDK::OccultationType IO::SDK::OccultationType::Full(std::string("FULL"));
IO::SDK::OccultationType IO::SDK::OccultationType::Annular(std::string("ANNULAR"));
IO::SDK::OccultationType IO::SDK::OccultationType::Partial(std::string("PARTIAL"));
IO::SDK::OccultationType IO::SDK::OccultationType::Any(std::string("ANY"));

IO::SDK::OccultationType::OccultationType(const std::string& name):m_name{name}
{

}

const char *IO::SDK::OccultationType::ToCharArray() const
{
    return m_name.c_str();
}