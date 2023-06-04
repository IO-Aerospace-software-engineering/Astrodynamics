#include <gtest/gtest.h>
#include <memory>
#include <CelestialBody.h>
#include <Constants.h>
#include <Site.h>
#include <Geodetic.h>
#include <DataPoolMonitoring.h>
#include <SiteFrameFile.h>
#include "TestParameters.h"

TEST(SiteFrame, Initialization)
{
    auto sun = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(10);
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399, sun);
    IO::Astrodynamics::Sites::Site s{399001, "S1", IO::Astrodynamics::Coordinates::Geodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD, 48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0), earth,std::string(SitePath)};
    auto id = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_S1_TOPO", 1);
    ASSERT_EQ(1399001, id[0]);

    auto name = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("FRAME_1399001_NAME", 1);
    ASSERT_STREQ("S1_TOPO", name[0].c_str());

    auto classVal = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_1399001_CLASS", 1);
    ASSERT_EQ(4, classVal[0]);

    auto classid = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_1399001_CLASS_ID", 1);
    ASSERT_EQ(1399001, classid[0]);

    auto centerid = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_1399001_CENTER", 1);
    ASSERT_EQ(399001, centerid[0]);

    auto spec = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_1399001_SPEC", 1);
    ASSERT_STREQ("ANGLES", spec[0].c_str());

    auto relative = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_1399001_RELATIVE", 1);
    ASSERT_STREQ("IAU_EARTH", relative[0].c_str());

    auto frameAngles = IO::Astrodynamics::DataPoolMonitoring::Instance().GetDoubleProperty("TKFRAME_1399001_ANGLES", 3);
    ASSERT_DOUBLE_EQ(-0.038397, frameAngles[0]);
    ASSERT_DOUBLE_EQ(-0.733038, frameAngles[1]);
    ASSERT_DOUBLE_EQ(3.1415926535897931, frameAngles[2]);

    auto axes = IO::Astrodynamics::DataPoolMonitoring::Instance().GetIntegerProperty("TKFRAME_1399001_AXES", 3);
    ASSERT_EQ(3, axes[0]);
    ASSERT_EQ(2, axes[1]);
    ASSERT_EQ(3, axes[2]);

    auto units = IO::Astrodynamics::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_1399001_UNITS", 1);
    ASSERT_STREQ("RADIANS", units[0].c_str());
}
