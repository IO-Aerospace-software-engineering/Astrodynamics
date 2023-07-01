/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include<OccultationType.h>
#include <SDKException.h>

IO::Astrodynamics::OccultationType IO::Astrodynamics::OccultationType::mFull(std::string("FULL"));
IO::Astrodynamics::OccultationType IO::Astrodynamics::OccultationType::mAnnular(std::string("ANNULAR"));
IO::Astrodynamics::OccultationType IO::Astrodynamics::OccultationType::mPartial(std::string("PARTIAL"));
IO::Astrodynamics::OccultationType IO::Astrodynamics::OccultationType::mAny(std::string("ANY"));

IO::Astrodynamics::OccultationType::OccultationType(std::string name) : m_name{std::move(name)}
{

}

const char *IO::Astrodynamics::OccultationType::ToCharArray() const
{
    return m_name.c_str();
}

IO::Astrodynamics::OccultationType &IO::Astrodynamics::OccultationType::Full()
{
    return mFull;
}

IO::Astrodynamics::OccultationType &IO::Astrodynamics::OccultationType::Annular()
{
    return mAnnular;
}

IO::Astrodynamics::OccultationType &IO::Astrodynamics::OccultationType::Partial()
{
    return mPartial;
}

IO::Astrodynamics::OccultationType &IO::Astrodynamics::OccultationType::Any()
{
    return mAny;
}

IO::Astrodynamics::OccultationType IO::Astrodynamics::OccultationType::ToOccultationType(const std::string &occultationType)
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

    throw IO::Astrodynamics::Exception::SDKException("Invalid occultation type : " + occultationType);
}