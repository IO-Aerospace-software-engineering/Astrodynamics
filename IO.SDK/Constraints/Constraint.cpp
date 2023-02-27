/**
 * @file Constraint.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "Constraint.h"

IO::SDK::Constraints::Constraint IO::SDK::Constraints::Constraint::_GreaterThan(std::string(">"));
IO::SDK::Constraints::Constraint IO::SDK::Constraints::Constraint::_LowerThan(std::string("<"));
IO::SDK::Constraints::Constraint IO::SDK::Constraints::Constraint::Equal(std::string("="));
IO::SDK::Constraints::Constraint IO::SDK::Constraints::Constraint::AbsMin(std::string("ABSMIN"));
IO::SDK::Constraints::Constraint IO::SDK::Constraints::Constraint::AbsMax(std::string("ABSMAX"));
IO::SDK::Constraints::Constraint IO::SDK::Constraints::Constraint::LocalMin(std::string("LOCMIN"));
IO::SDK::Constraints::Constraint IO::SDK::Constraints::Constraint::LocalMax(std::string("LOCMAX"));

IO::SDK::Constraints::Constraint::Constraint(const std::string& name) : m_name{name}
{
}

const char *IO::SDK::Constraints::Constraint::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::Constraints::Constraint& IO::SDK::Constraints::Constraint::GreaterThan()
{
    return _GreaterThan;
}

IO::SDK::Constraints::Constraint& IO::SDK::Constraints::Constraint::LowerThan()
{
    return _LowerThan;
}