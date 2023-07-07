//
// Created by s.guillet on 07/07/2023.
//
#include <gtest/gtest.h>
#include <Barycenter.h>


TEST(Barycenter, CreateBarycenter)
{
    IO::Astrodynamics::Body::Barycenter ssb(0);
    ASSERT_STREQ("SOLAR SYSTEM BARYCENTER", ssb.GetName().c_str());
    ASSERT_DOUBLE_EQ(1.328905186666226e+20, ssb.GetMu());
    ASSERT_EQ(0, ssb.GetId());
}