/**
 * @file OccultationType.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include<OccultationType.h>
#include "SDKException.h"

IO::SDK::OccultationType IO::SDK::OccultationType::mFull(std::string("FULL"));
IO::SDK::OccultationType IO::SDK::OccultationType::mAnnular(std::string("ANNULAR"));
IO::SDK::OccultationType IO::SDK::OccultationType::mPartial(std::string("PARTIAL"));
IO::SDK::OccultationType IO::SDK::OccultationType::mAny(std::string("ANY"));

IO::SDK::OccultationType::OccultationType(std::string name) : m_name{std::move(name)}
{

}

const char *IO::SDK::OccultationType::ToCharArray() const
{
    return m_name.c_str();
}

IO::SDK::OccultationType &IO::SDK::OccultationType::Full()
{
    return mFull;
}

IO::SDK::OccultationType &IO::SDK::OccultationType::Annular()
{
    return mAnnular;
}

IO::SDK::OccultationType &IO::SDK::OccultationType::Partial()
{
    return mPartial;
}

IO::SDK::OccultationType &IO::SDK::OccultationType::Any()
{
    return mAny;
}

IO::SDK::OccultationType IO::SDK::OccultationType::ToOccultationType(const std::string &occultationType)
{
    if (occultationType == OccultationType::mAnnular.ToCharArray())
    {
        return mAnnular;
    } else if (occultationType == OccultationType::mAny.ToCharArray())
    {
        return mAny;
    }else if(occultationType==OccultationType::mFull.ToCharArray())
    {
        return  mFull;
    }else if(occultationType==OccultationType::mPartial.ToCharArray())
    {
        return mPartial;
    }

    throw IO::SDK::Exception::SDKException("Invalid occultation type : " + occultationType);
}