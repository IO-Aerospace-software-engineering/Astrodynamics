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

IO::SDK::OccultationType IO::SDK::OccultationType::_Full(std::string("FULL"));
IO::SDK::OccultationType IO::SDK::OccultationType::_Annular(std::string("ANNULAR"));
IO::SDK::OccultationType IO::SDK::OccultationType::_Partial(std::string("PARTIAL"));
IO::SDK::OccultationType IO::SDK::OccultationType::_Any(std::string("ANY"));

IO::SDK::OccultationType::OccultationType(const std::string& name):m_name{name}
{

}

const char *IO::SDK::OccultationType::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::OccultationType& IO::SDK::OccultationType::Full()
{
    return _Full;
}

IO::SDK::OccultationType& IO::SDK::OccultationType::Annular()
{
    return _Annular;
}

IO::SDK::OccultationType& IO::SDK::OccultationType::Partial()
{
    return _Partial;
}

IO::SDK::OccultationType& IO::SDK::OccultationType::Any()
{
    return _Any;
}