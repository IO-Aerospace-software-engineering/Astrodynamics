/**
 * @file RelationalOperator.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include "RelationalOperator.h"

#include <utility>
#include <SDKException.h>

IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::mGreaterThan(std::string(">"));
IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::mLowerThan(std::string("<"));
IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::mEqual(std::string("="));
IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::mAbsMin(std::string("ABSMIN"));
IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::mAbsMax(std::string("ABSMAX"));
IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::mLocalMin(std::string("LOCMIN"));
IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::mLocalMax(std::string("LOCMAX"));

IO::SDK::Constraints::RelationalOperator::RelationalOperator(std::string name) : m_name{std::move(name)}
{
}

const char *IO::SDK::Constraints::RelationalOperator::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::GreaterThan()
{
    return mGreaterThan;
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::LowerThan()
{
    return mLowerThan;
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::Equal()
{
    return mEqual;
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::AbsMin()
{
    return mAbsMin;
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::AbsMax()
{
    return mAbsMax;
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::LocalMin()
{
    return mLocalMin;
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::LocalMax()
{
    return mLocalMax;
}

IO::SDK::Constraints::RelationalOperator &IO::SDK::Constraints::RelationalOperator::operator=(const RelationalOperator &other)
{
    if (this == &other)
        return *this;
    const_cast<std::string &>(m_name) = other.m_name;
    return *this;
}

IO::SDK::Constraints::RelationalOperator IO::SDK::Constraints::RelationalOperator::ToRelationalOperator(const std::string &relationalOperator)
{
    if (relationalOperator == IO::SDK::Constraints::RelationalOperator::Equal().ToCharArray())
    {
        return IO::SDK::Constraints::RelationalOperator::Equal();
    } else if (relationalOperator == IO::SDK::Constraints::RelationalOperator::LowerThan().ToCharArray())
    {
        return IO::SDK::Constraints::RelationalOperator::LowerThan();
    } else if (relationalOperator == IO::SDK::Constraints::RelationalOperator::GreaterThan().ToCharArray())
    {
        return IO::SDK::Constraints::RelationalOperator::GreaterThan();
    } else if (relationalOperator == IO::SDK::Constraints::RelationalOperator::AbsMin().ToCharArray())
    {
        return IO::SDK::Constraints::RelationalOperator::AbsMin();
    } else if (relationalOperator == IO::SDK::Constraints::RelationalOperator::AbsMax().ToCharArray())
    {
        return IO::SDK::Constraints::RelationalOperator::AbsMax();
    } else if (relationalOperator == IO::SDK::Constraints::RelationalOperator::LocalMin().ToCharArray())
    {
        return IO::SDK::Constraints::RelationalOperator::LocalMin();
    } else if (relationalOperator == IO::SDK::Constraints::RelationalOperator::LocalMax().ToCharArray())
    {
        return IO::SDK::Constraints::RelationalOperator::LocalMax();
    }

    throw IO::SDK::Exception::SDKException("Invalid relational operator : " + relationalOperator);
}