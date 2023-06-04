/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <RelationalOperator.h>
#include <SDKException.h>

IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::mGreaterThan(std::string(">"));
IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::mLowerThan(std::string("<"));
IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::mEqual(std::string("="));
IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::mAbsMin(std::string("ABSMIN"));
IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::mAbsMax(std::string("ABSMAX"));
IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::mLocalMin(std::string("LOCMIN"));
IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::mLocalMax(std::string("LOCMAX"));

IO::Astrodynamics::Constraints::RelationalOperator::RelationalOperator(std::string name) : m_name{std::move(name)}
{
}

const char *IO::Astrodynamics::Constraints::RelationalOperator::ToCharArray() const
{
    return m_name.c_str();
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::GreaterThan()
{
    return mGreaterThan;
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::LowerThan()
{
    return mLowerThan;
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::Equal()
{
    return mEqual;
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::AbsMin()
{
    return mAbsMin;
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::AbsMax()
{
    return mAbsMax;
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::LocalMin()
{
    return mLocalMin;
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::LocalMax()
{
    return mLocalMax;
}

IO::Astrodynamics::Constraints::RelationalOperator &IO::Astrodynamics::Constraints::RelationalOperator::operator=(const RelationalOperator &other)
{
    if (this == &other)
        return *this;
    const_cast<std::string &>(m_name) = other.m_name;
    return *this;
}

IO::Astrodynamics::Constraints::RelationalOperator IO::Astrodynamics::Constraints::RelationalOperator::ToRelationalOperator(const std::string &relationalOperator)
{
    if (relationalOperator == IO::Astrodynamics::Constraints::RelationalOperator::Equal().ToCharArray())
    {
        return IO::Astrodynamics::Constraints::RelationalOperator::Equal();
    } else if (relationalOperator == IO::Astrodynamics::Constraints::RelationalOperator::LowerThan().ToCharArray())
    {
        return IO::Astrodynamics::Constraints::RelationalOperator::LowerThan();
    } else if (relationalOperator == IO::Astrodynamics::Constraints::RelationalOperator::GreaterThan().ToCharArray())
    {
        return IO::Astrodynamics::Constraints::RelationalOperator::GreaterThan();
    } else if (relationalOperator == IO::Astrodynamics::Constraints::RelationalOperator::AbsMin().ToCharArray())
    {
        return IO::Astrodynamics::Constraints::RelationalOperator::AbsMin();
    } else if (relationalOperator == IO::Astrodynamics::Constraints::RelationalOperator::AbsMax().ToCharArray())
    {
        return IO::Astrodynamics::Constraints::RelationalOperator::AbsMax();
    } else if (relationalOperator == IO::Astrodynamics::Constraints::RelationalOperator::LocalMin().ToCharArray())
    {
        return IO::Astrodynamics::Constraints::RelationalOperator::LocalMin();
    } else if (relationalOperator == IO::Astrodynamics::Constraints::RelationalOperator::LocalMax().ToCharArray())
    {
        return IO::Astrodynamics::Constraints::RelationalOperator::LocalMax();
    }

    throw IO::Astrodynamics::Exception::SDKException("Invalid relational operator : " + relationalOperator);
}