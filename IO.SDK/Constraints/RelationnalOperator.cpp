/**
 * @file RelationnalOperator.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "RelationnalOperator.h"

#include <utility>

IO::SDK::Constraints::RelationnalOperator IO::SDK::Constraints::RelationnalOperator::mGreaterThan(std::string(">"));
IO::SDK::Constraints::RelationnalOperator IO::SDK::Constraints::RelationnalOperator::mLowerThan(std::string("<"));
IO::SDK::Constraints::RelationnalOperator IO::SDK::Constraints::RelationnalOperator::mEqual(std::string("="));
IO::SDK::Constraints::RelationnalOperator IO::SDK::Constraints::RelationnalOperator::mAbsMin(std::string("ABSMIN"));
IO::SDK::Constraints::RelationnalOperator IO::SDK::Constraints::RelationnalOperator::mAbsMax(std::string("ABSMAX"));
IO::SDK::Constraints::RelationnalOperator IO::SDK::Constraints::RelationnalOperator::mLocalMin(std::string("LOCMIN"));
IO::SDK::Constraints::RelationnalOperator IO::SDK::Constraints::RelationnalOperator::mLocalMax(std::string("LOCMAX"));

IO::SDK::Constraints::RelationnalOperator::RelationnalOperator(std::string  name) : m_name{std::move(name)}
{
}

const char *IO::SDK::Constraints::RelationnalOperator::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::Constraints::RelationnalOperator& IO::SDK::Constraints::RelationnalOperator::GreaterThan()
{
    return mGreaterThan;
}

IO::SDK::Constraints::RelationnalOperator& IO::SDK::Constraints::RelationnalOperator::LowerThan()
{
    return mLowerThan;
}

IO::SDK::Constraints::RelationnalOperator &IO::SDK::Constraints::RelationnalOperator::Equal()
{
    return mEqual;
}

IO::SDK::Constraints::RelationnalOperator &IO::SDK::Constraints::RelationnalOperator::AbsMin()
{
    return mAbsMin;
}

IO::SDK::Constraints::RelationnalOperator &IO::SDK::Constraints::RelationnalOperator::AbsMax()
{
    return mAbsMax;
}

IO::SDK::Constraints::RelationnalOperator &IO::SDK::Constraints::RelationnalOperator::LocalMin()
{
    return mLocalMin;
}

IO::SDK::Constraints::RelationnalOperator &IO::SDK::Constraints::RelationnalOperator::LocalMax()
{
    return mLocalMax;
}
