#include <gtest/gtest.h>
#include <OccultationType.h>

TEST(OccultationTypes, Values)
{
    
    ASSERT_STREQ("ANNULAR", IO::Astrodynamics::OccultationType::Annular().ToCharArray());
    ASSERT_STREQ("PARTIAL", IO::Astrodynamics::OccultationType::Partial().ToCharArray());
    ASSERT_STREQ("FULL", IO::Astrodynamics::OccultationType::Full().ToCharArray());
    ASSERT_STREQ("ANY", IO::Astrodynamics::OccultationType::Any().ToCharArray());
}