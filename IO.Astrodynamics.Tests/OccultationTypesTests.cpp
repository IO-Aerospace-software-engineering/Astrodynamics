#include <gtest/gtest.h>
#include <OccultationType.h>

TEST(OccultationTypes, Values)
{
    
    ASSERT_STREQ("ANNULAR", IO::SDK::OccultationType::Annular().ToCharArray());
    ASSERT_STREQ("PARTIAL", IO::SDK::OccultationType::Partial().ToCharArray());
    ASSERT_STREQ("FULL", IO::SDK::OccultationType::Full().ToCharArray());
    ASSERT_STREQ("ANY", IO::SDK::OccultationType::Any().ToCharArray());
}