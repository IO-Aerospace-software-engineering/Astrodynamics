#include <gtest/gtest.h>
#include <memory>
#include <CelestialBody.h>
#include <Constants.h>
#include <Site.h>
#include <Geodetic.h>
#include<DataPoolMonitoring.h>

TEST(SiteFrame, Initialization)
{
    auto sun = std::make_shared<IO::SDK::Body::CelestialBody>(10, "sun");
    auto earth = std::make_shared<IO::SDK::Body::CelestialBody>(399, "earth", sun);
    IO::SDK::Sites::Site s{123456, "S1", IO::SDK::Coordinates::Geodetic(2.2 * IO::SDK::Constants::DEG_RAD, 48.0 * IO::SDK::Constants::DEG_RAD, 0.0), earth};
    auto id = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_S1_TOPO", 1);
    ASSERT_EQ(1522456, id[0]);

    auto name = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("FRAME_1522456_NAME", 1);
    ASSERT_STREQ("S1_TOPO", name[0].c_str());

    auto classVal = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_1522456_CLASS", 1);
    ASSERT_EQ(4, classVal[0]);

    auto classid = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_1522456_CLASS_ID", 1);
    ASSERT_EQ(1522456, classid[0]);

    auto centerid = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("FRAME_1522456_CENTER", 1);
    ASSERT_EQ(522456, centerid[0]);

    auto spec = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_1522456_SPEC", 1);
    ASSERT_STREQ("ANGLES", spec[0].c_str());

    auto relative = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_1522456_RELATIVE", 1);
    ASSERT_STREQ("IAU_earth", relative[0].c_str());

    auto frameAngles = IO::SDK::DataPoolMonitoring::Instance().GetDoubleProperty("TKFRAME_1522456_ANGLES", 3);
    ASSERT_DOUBLE_EQ(-0.038397, frameAngles[0]);
    ASSERT_DOUBLE_EQ(-0.733038, frameAngles[1]);
    ASSERT_DOUBLE_EQ(3.1415926535897931, frameAngles[2]);

    auto axes = IO::SDK::DataPoolMonitoring::Instance().GetIntegerProperty("TKFRAME_1522456_AXES", 3);
    ASSERT_EQ(3, axes[0]);
    ASSERT_EQ(2, axes[1]);
    ASSERT_EQ(3, axes[2]);

    auto units = IO::SDK::DataPoolMonitoring::Instance().GetStringProperty("TKFRAME_1522456_UNITS", 1);
    ASSERT_STREQ("RADIANS", units[0].c_str());
}