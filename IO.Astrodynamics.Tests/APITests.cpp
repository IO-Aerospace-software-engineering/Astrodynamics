/*
 Copyright (c) 2023-2024. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#include<gtest/gtest.h>
#include "TestParameters.h"
#include "Proxy.h"
#include "InertialFrames.h"
#include "Spacecraft.h"
#include <Converters.cpp>
#include <TLE.h>
#include <Constants.h>
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iostream>
#include <locale>
#include <sstream>
#include <string>
#include <vector>

TEST(API, TDBToString)
{
    auto res = TDBToStringProxy(0.0);
    ASSERT_STREQ("2000-01-01 12:00:00.000000 (TDB)", res);
    free((void*)res);
}

TEST(API, UTCToString)
{
    auto res = UTCToStringProxy(0.0);
    ASSERT_STREQ("2000-01-01 12:00:00.000000 (UTC)", res);
    free((void*)res);
}

TEST(API, FindWindowsOnCoordinateConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 730036800.0;
    searchWindow.end = 730123200;
    ASSERT_TRUE(FindWindowsOnCoordinateConstraintProxy(searchWindow, 399013, 301, "DSS-13_TOPO", "LATITUDINAL", "LATITUDE", ">",
                                           0.0, 0.0, "NONE", 60.0,
                                           windows));

    ASSERT_STREQ("2023-02-19 14:33:08.917987 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2023-02-19 23:58:50.814787 (UTC)", ToTDBWindow(windows[0]).GetEndDate().ToUTC().ToString().c_str());
}

TEST(API, FindWindowsOnDistanceConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::Astrodynamics::Time::TDB("2007 JAN 1").GetSecondsFromJ2000().count();
    searchWindow.end = IO::Astrodynamics::Time::TDB("2007 APR 1").GetSecondsFromJ2000().count();
    ASSERT_TRUE(FindWindowsOnDistanceConstraintProxy(searchWindow, 399, 301, ">", 400000000, "NONE", 86400.0, windows));

    ASSERT_STREQ("2007-01-08 00:11:07.628591 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-01-13 06:37:47.948144 (TDB)", ToTDBWindow(windows[0]).GetEndDate().ToString().c_str());
    ASSERT_STREQ("2007-03-29 22:53:58.151896 (TDB)", ToTDBWindow(windows[3]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2007-04-01 00:01:05.185654 (TDB)", ToTDBWindow(windows[3]).GetEndDate().ToString().c_str());
}

TEST(API, FindWindowsOnIlluminationConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::Astrodynamics::Time::TDB("2021-05-17 12:00:00 TDB").GetSecondsFromJ2000().count();
    searchWindow.end = IO::Astrodynamics::Time::TDB("2021-05-18 12:00:00 TDB").GetSecondsFromJ2000().count();
    IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD,
                                                          48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0);
    ASSERT_TRUE(FindWindowsOnIlluminationConstraintProxy(searchWindow, 10, "SUN", 399, "IAU_EARTH",
                                             geodetic, "INCIDENCE", "<",
                                             IO::Astrodynamics::Constants::PI2 -
                                             IO::Astrodynamics::Constants::OfficialTwilight, 0.0,
                                             "CN+S", 4.5 * 60 * 60, "Ellipsoid", windows));

    ASSERT_STREQ("2021-05-17 12:00:00.000000 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2021-05-17 19:34:33.699813 (UTC)", ToTDBWindow(windows[0]).GetEndDate().ToUTC().ToString().c_str());
    ASSERT_STREQ("2021-05-18 04:17:40.875540 (UTC)", ToTDBWindow(windows[1]).GetStartDate().ToUTC().ToString().c_str());
    ASSERT_STREQ("2021-05-18 12:00:00.000000 (TDB)", ToTDBWindow(windows[1]).GetEndDate().ToString().c_str());
}

TEST(API, FindWindowsOnOccultationConstraintProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = IO::Astrodynamics::Time::TDB("2001 DEC 13").GetSecondsFromJ2000().count();
    searchWindow.end = IO::Astrodynamics::Time::TDB("2001 DEC 15").GetSecondsFromJ2000().count();
    ASSERT_TRUE(FindWindowsOnOccultationConstraintProxy(searchWindow, 399, 10, "IAU_SUN", "ELLIPSOID", 301, "IAU_MOON", "ELLIPSOID",
                                            "ANY", "LT", 3600.0, windows));

    ASSERT_STREQ("2001-12-14 20:10:15.410588 (TDB)", ToTDBWindow(windows[0]).GetStartDate().ToString().c_str());
    ASSERT_STREQ("2001-12-14 21:35:49.100520 (TDB)", ToTDBWindow(windows[0]).GetEndDate().ToString().c_str());
}


// A search SPICE cannot run returns false with SPICE's message, and writes no window (fourth round of #668: before, the
// proxies printed the error, dropped it and returned no window, which a caller could not tell from a condition that never
// holds)
namespace
{
    IO::Astrodynamics::API::DTO::WindowDTO SearchWindow(const char *start, const char *end)
    {
        IO::Astrodynamics::API::DTO::WindowDTO window{};
        window.start = IO::Astrodynamics::Time::TDB(start).GetSecondsFromJ2000().count();
        window.end = IO::Astrodynamics::Time::TDB(end).GetSecondsFromJ2000().count();
        return window;
    }

    // Windows pre-filled with a sentinel, which a failed search must leave as it is
    void FillWindows(IO::Astrodynamics::API::DTO::WindowDTO windows[1000])
    {
        for (int i = 0; i < 1000; ++i)
        {
            windows[i].start = -1.0;
            windows[i].end = -1.0;
        }
    }

    void AssertNoWindowWritten(const IO::Astrodynamics::API::DTO::WindowDTO windows[1000])
    {
        for (int i = 0; i < 1000; ++i)
        {
            ASSERT_EQ(-1.0, windows[i].start);
            ASSERT_EQ(-1.0, windows[i].end);
        }
    }
}

TEST(API, FindWindowsOnCoordinateConstraintProxyUnknownFrame)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    FillWindows(windows);
    ASSERT_FALSE(FindWindowsOnCoordinateConstraintProxy(SearchWindow("2024 MAR 1", "2024 MAR 10"), 399, 301, "NOT_A_FRAME_668",
                                                        "RECTANGULAR", "Z", ">", 0.0, 0.0, "NONE", 3600.0, windows));
    ASSERT_NE(nullptr, std::strstr(GetLastErrorProxy(), "NOT_A_FRAME_668"));
    ASSERT_NE(nullptr, std::strstr(GetLastErrorProxy(), "not recognized"));
    AssertNoWindowWritten(windows);
    // The error is reset: a valid search after it runs
    ASSERT_TRUE(FindWindowsOnCoordinateConstraintProxy(SearchWindow("2024 MAR 1", "2024 MAR 10"), 399, 301, "J2000",
                                                       "RECTANGULAR", "Z", "<", 0.0, 0.0, "NONE", 3600.0, windows));
}

TEST(API, FindWindowsOnDistanceConstraintProxyOutOfCoverage)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    FillWindows(windows);
    ASSERT_FALSE(FindWindowsOnDistanceConstraintProxy(SearchWindow("2160 MAR 1", "2160 MAR 10"), 399, 301, ">", 400000000.0,
                                                      "NONE", 3600.0, windows));
    ASSERT_NE(nullptr, std::strstr(GetLastErrorProxy(), "Insufficient ephemeris data"));
    AssertNoWindowWritten(windows);
}

TEST(API, FindWindowsOnOccultationConstraintProxyOutOfCoverage)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    FillWindows(windows);
    ASSERT_FALSE(FindWindowsOnOccultationConstraintProxy(SearchWindow("2160 MAR 1", "2160 MAR 10"), 399, 10, "IAU_SUN", "ELLIPSOID",
                                                         301, "IAU_MOON", "ELLIPSOID", "ANY", "LT", 3600.0, windows));
    ASSERT_NE(nullptr, std::strstr(GetLastErrorProxy(), "2160"));
    AssertNoWindowWritten(windows);
}

TEST(API, FindWindowsOnIlluminationConstraintProxyUnknownFrame)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    FillWindows(windows);
    IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD,
                                                          48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0);
    ASSERT_FALSE(FindWindowsOnIlluminationConstraintProxy(SearchWindow("2024 MAR 1", "2024 MAR 10"), 301, "SUN", 399,
                                                          "NOT_A_FRAME_668", geodetic, "INCIDENCE", "<",
                                                          IO::Astrodynamics::Constants::PI2, 0.0, "NONE", 3600.0, "Ellipsoid",
                                                          windows));
    ASSERT_NE(nullptr, std::strstr(GetLastErrorProxy(), "NOT_A_FRAME_668"));
    AssertNoWindowWritten(windows);
}

TEST(API, FindWindowsInFieldOfViewConstraintProxyWithoutIk)
{
    IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
    FillWindows(windows);
    ASSERT_FALSE(FindWindowsInFieldOfViewConstraintProxy(SearchWindow("2024 MAR 1", "2024 MAR 10"), -999, -999123, 301,
                                                         "IAU_MOON", "ELLIPSOID", "NONE", 3600.0, windows));
    ASSERT_NE(nullptr, std::strstr(GetLastErrorProxy(), "INS-999123_FOV_FRAME"));
    AssertNoWindowWritten(windows);
}


TEST(API, ReadEphemerisProxy)
{
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 0.0;
    searchWindow.end = 100.0;

    IO::Astrodynamics::API::DTO::StateVectorDTO sv[5000];
    ASSERT_TRUE(ReadEphemerisProxy(searchWindow, 399, 301, "J2000", "LT", 10.0, sv));
    ASSERT_DOUBLE_EQ(-291569264.48965073, sv[0].position.x);
    ASSERT_DOUBLE_EQ(-266709187.1624887, sv[0].position.y);
    ASSERT_DOUBLE_EQ(-76099155.244104564, sv[0].position.z);
    ASSERT_DOUBLE_EQ(643.53061483971885, sv[0].velocity.x);
    ASSERT_DOUBLE_EQ(-666.08181440799092, sv[0].velocity.y);
    ASSERT_DOUBLE_EQ(-301.32283209101018, sv[0].velocity.z);
    ASSERT_DOUBLE_EQ(399, sv[0].centerOfMotionId);
    ASSERT_STREQ("J2000", sv[0].inertialFrame);
    ASSERT_DOUBLE_EQ(0.0, sv[0].epoch);
}

TEST(API, ReadEphemerisProxyException)
{
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 0.0;
    searchWindow.end = 10001.0;

    IO::Astrodynamics::API::DTO::StateVectorDTO sv[5000];
    ASSERT_FALSE(ReadEphemerisProxy(searchWindow, 399, 301, "J2000", "LT", 1.0, sv));
    ASSERT_STRNE("", GetLastErrorProxy());
}

TEST(API, ReadSpacecraftOrientationProxyException)
{
    IO::Astrodynamics::API::DTO::WindowDTO searchWindow{};
    searchWindow.start = 0.0;
    searchWindow.end = 10001.0;

    IO::Astrodynamics::API::DTO::StateOrientationDTO so[10000];
    ASSERT_FALSE(ReadOrientationProxy(searchWindow, -172, 10.0 * std::pow(2.0, ClockAccuracy), "J2000", 1.0, so));
    ASSERT_STRNE("", GetLastErrorProxy());
}

TEST(API, ToTDB)
{
    ASSERT_DOUBLE_EQ(64.183927284669423, ConvertUTCToTDBProxy(0.0));
}

TEST(API, ToUTC)
{
    ASSERT_DOUBLE_EQ(-64.183927263223808, ConvertTDBToUTCProxy(0.0));
}

TEST(API, Version)
{
    const char* res = GetSpiceVersionProxy();
    ASSERT_STREQ("CSPICE_N0067", res);
    free((void*)res);
}

TEST(API, WriteEphemeris)
{
    const int size = 10;
    IO::Astrodynamics::API::DTO::StateVectorDTO sv[size];
    for (int i = 0; i < size; ++i)
    {
        sv[i].position.x = 6800 + i;
        sv[i].position.y = i;
        sv[i].position.z = i;
        sv[i].velocity.x = i;
        sv[i].velocity.y = 8.0 + i * 0.001;
        sv[i].velocity.z = i;
        sv[i].epoch = i;
        sv[i].centerOfMotionId = 399;
        sv[i].SetFrame("J2000");
    }

    //Write ephemeris file
    WriteEphemerisProxy("EphemerisTestFile.spk", -135, sv, size);

    //Load ephemeris file
    LoadKernelsProxy("EphemerisTestFile.spk");

    IO::Astrodynamics::API::DTO::WindowDTO window{};
    window.start = 0.0;
    window.end = 9.0;
    IO::Astrodynamics::API::DTO::StateVectorDTO svresult[size];
    ReadEphemerisProxy(window, 399, -135, "J2000", "NONE", 1.0, svresult);
    for (int i = 0; i < size; ++i)
    {
        ASSERT_DOUBLE_EQ(svresult[i].position.x, 6800 + i);
        ASSERT_DOUBLE_EQ(svresult[i].position.y, i);
        ASSERT_DOUBLE_EQ(svresult[i].position.z, i);
        ASSERT_DOUBLE_EQ(svresult[i].velocity.x, i);
        ASSERT_DOUBLE_EQ(svresult[i].velocity.y, 8 + i * 0.001);
        ASSERT_DOUBLE_EQ(svresult[i].velocity.z, i);
        ASSERT_DOUBLE_EQ(svresult[i].epoch, i);
        ASSERT_EQ(svresult[i].centerOfMotionId, 399);
        ASSERT_STREQ(svresult[i].inertialFrame, "J2000");
    }
}

TEST(API, WriteOrientation)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(earth,
                                                           IO::Astrodynamics::Math::Vector3D(
                                                               6800000.0,
                                                               0.0, 0.0),
                                                           IO::Astrodynamics::Math::Vector3D(
                                                               0.0,
                                                               8000.0,
                                                               0.0),
                                                           IO::Astrodynamics::Time::TDB(
                                                               0.0s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-175, "SPC000", 1000.0, 3000.0, std::string(SpacecraftPath),
                                                        std::move(orbitalParams));
    const int size = 10;
    IO::Astrodynamics::API::DTO::StateOrientationDTO sv[size];
    for (int i = 0; i < size; ++i)
    {
        sv[i].orientation.w = i;
        sv[i].orientation.x = 1 + i * 0.1;
        sv[i].orientation.y = 1 + i * 0.2;
        sv[i].orientation.z = 1 + i * 0.3;
        sv[i].angularVelocity.x = 0.0;
        sv[i].angularVelocity.y = 0.0;
        sv[i].angularVelocity.z = 0.0;
        sv[i].epoch = i;
        sv[i].SetFrame("J2000");
    }

    //Write ephemeris file
    WriteOrientationProxy((std::string(SpacecraftPath) + "/OrientationTestFile.ck").c_str(), -175, sv, size);

    //Load ephemeris file
    LoadKernelsProxy((std::string(SpacecraftPath) + "/OrientationTestFile.ck").c_str());

    IO::Astrodynamics::API::DTO::WindowDTO window{};
    window.start = 0.0;
    window.end = 9.0;
    IO::Astrodynamics::API::DTO::StateOrientationDTO soresult[size];
    ReadOrientationProxy(window, -175, 0.0, "J2000", 1.0, soresult);

    ASSERT_DOUBLE_EQ(soresult[0].orientation.w, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].orientation.x, -0.57735026918962573);
    ASSERT_DOUBLE_EQ(soresult[0].orientation.y, -0.57735026918962573);
    ASSERT_DOUBLE_EQ(soresult[0].orientation.z, -0.57735026918962573);
    ASSERT_DOUBLE_EQ(soresult[0].angularVelocity.x, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].angularVelocity.y, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].angularVelocity.z, 0.0);
    ASSERT_DOUBLE_EQ(soresult[0].epoch, 0);
    ASSERT_STREQ(soresult[0].frame, "J2000");

    ASSERT_DOUBLE_EQ(soresult[4].orientation.w, 0.78386180166962049);
    ASSERT_DOUBLE_EQ(soresult[4].orientation.x, 0.27435163058436718);
    ASSERT_DOUBLE_EQ(soresult[4].orientation.y, 0.35273781075132921);
    ASSERT_DOUBLE_EQ(soresult[4].orientation.z, 0.43112399091829129);
    ASSERT_DOUBLE_EQ(soresult[4].angularVelocity.x, 0.0);
    ASSERT_DOUBLE_EQ(soresult[4].angularVelocity.y, 0.0);
    ASSERT_DOUBLE_EQ(soresult[4].angularVelocity.z, 0.0);
    ASSERT_DOUBLE_EQ(soresult[4].epoch, 4);
    ASSERT_STREQ(soresult[4].frame, "J2000");

    ASSERT_DOUBLE_EQ(soresult[9].orientation.w, 0.87358057364767872);
    ASSERT_DOUBLE_EQ(soresult[9].orientation.x, 0.18442256554784328);
    ASSERT_DOUBLE_EQ(soresult[9].orientation.y, 0.27178062291261118);
    ASSERT_DOUBLE_EQ(soresult[9].orientation.z, 0.359138680277379);
    ASSERT_DOUBLE_EQ(soresult[9].angularVelocity.x, 0.0);
    ASSERT_DOUBLE_EQ(soresult[9].angularVelocity.y, 0.0);
    ASSERT_DOUBLE_EQ(soresult[9].angularVelocity.z, 0.0);
    ASSERT_DOUBLE_EQ(soresult[9].epoch, 9);
    ASSERT_STREQ(soresult[9].frame, "J2000");
}

TEST(API, GetBodyInformation)
{
    auto res = GetCelestialBodyInfoProxy(399);
    ASSERT_EQ(399, res.Id);
    ASSERT_EQ(10, res.CenterOfMotionId);
    ASSERT_STREQ("EARTH", res.Name);
    ASSERT_EQ(13000, res.FrameId);
    ASSERT_STREQ("ITRF93", res.FrameName);
    ASSERT_DOUBLE_EQ(398600435436095.94, res.GM);
    ASSERT_DOUBLE_EQ(6378136.5999999998, res.Radii.x);
    ASSERT_DOUBLE_EQ(6378136.5999999998, res.Radii.y);
    ASSERT_DOUBLE_EQ(6356751.9000000002, res.Radii.z);
    ASSERT_DOUBLE_EQ(0.00108262998905, res.J2);
    ASSERT_DOUBLE_EQ(-2.5321530600000001e-06, res.J3);
    ASSERT_DOUBLE_EQ(-1.6109876100000001e-06, res.J4);
}

TEST(API, GetBodyInformationWithoutJ)
{
    auto res = GetCelestialBodyInfoProxy(301);
    ASSERT_EQ(301, res.Id);
    ASSERT_EQ(399, res.CenterOfMotionId);
    ASSERT_STREQ("MOON", res.Name);
    ASSERT_EQ(31001, res.FrameId);
    ASSERT_STREQ("MOON_ME", res.FrameName);
    ASSERT_DOUBLE_EQ(4902800066163.7959, res.GM);
    ASSERT_DOUBLE_EQ(1737400, res.Radii.x);
    ASSERT_DOUBLE_EQ(1737400, res.Radii.y);
    ASSERT_DOUBLE_EQ(1737400, res.Radii.z);
    ASSERT_TRUE(std::isnan(res.J2));
    ASSERT_TRUE(std::isnan(res.J3));
    ASSERT_TRUE(std::isnan(res.J4));
}

// What the library reads from the kernel pool is checked against the text of the kernels of Data/SolarSystem, as
// KernelTextValues does in the .NET tests (#672). The test executable links no CSPICE of its own (IO.Astrodynamics links it
// PRIVATE, #668 second round; before, on Windows, where the DLL exports none of cspice.lib, it linked its own copy, whose
// kernel pool was empty and whose error state was its own). The built-in body names and frames these tests expect are NAIF's
// (Frames Required Reading, PCK-based frames built into SPICE; NAIF IDs Required Reading), asserted as literals on every
// platform; on Linux and macOS, where the shared library exports the CSPICE routines it contains, they are also read through
// cnmfrm_c and bodc2n_c, which bind to the library's own CSPICE. The Windows DLL exports only the proxies, so these calls are
// not compiled there.

// The values of a kernel variable in the data of a text kernel of Data/SolarSystem, read without SPICE: an assignment "="
// replaces them, "+=" appends to them; the values, in parentheses or not, separated by blanks or commas, a Fortran exponent
// D read as E. Empty when the kernel does not assign the variable
static std::vector<double> KernelTextValues(const std::string &kernel, const std::string &name)
{
    std::ifstream file(std::string(SolarSystemKernelPath) + "/" + kernel);
    if (!file.is_open())
    {
        ADD_FAILURE() << "Kernel not found: " << kernel;
        return {};
    }
    std::vector<double> values;
    bool inData{false};
    bool inValues{false};
    bool inParentheses{false};
    std::string line;
    while (std::getline(file, line))
    {
        const auto first = line.find_first_not_of(" \t\r");
        const std::string content = first == std::string::npos ? std::string{} : line.substr(first);
        if (content.rfind("\\begindata", 0) == 0 || content.rfind("\\begintext", 0) == 0)
        {
            inData = content.rfind("\\begindata", 0) == 0;
            continue;
        }
        if (!inData)
        {
            continue;
        }
        std::string text;
        if (inValues)
        {
            text = content;
        } else
        {
            const auto end = content.find_first_of(" \t+=");
            if (content.substr(0, end) != name)
            {
                continue;
            }
            const auto equal = content.find('=', end);
            if (equal == std::string::npos)
            {
                continue;
            }
            if (content.find('+', end) >= equal)
            {
                values.clear();
            }
            text = content.substr(equal + 1);
            const auto open = text.find_first_not_of(" \t\r");
            inParentheses = open != std::string::npos && text[open] == '(';
            inValues = true;
        }
        const auto close = text.find(')');
        inValues = inParentheses && close == std::string::npos;
        text = text.substr(0, close);
        for (auto &c: text)
        {
            if (c == '(' || c == ',' || c == '\r')
            {
                c = ' ';
            } else if (c == 'D' || c == 'd')
            {
                c = 'E';
            }
        }
        std::istringstream stream(text);
        stream.imbue(std::locale::classic());
        double value{};
        while (stream >> value)
        {
            values.push_back(value);
        }
    }
    return values;
}

// The number of doubles between two finite doubles of the same sign (0 when equal)
static std::int64_t UlpDistance(double a, double b)
{
    if (a == b)
    {
        return 0;
    }
    std::int64_t x{}, y{};
    std::memcpy(&x, &a, sizeof a);
    std::memcpy(&y, &b, sizeof b);
    return std::llabs(x - y);
}

// The bound, in ulp, of a GM the library reads through CSPICE against the kernel text parsed by the C++ library (correctly
// rounded): CSPICE's parse of the decimal text is not. bodvcd_c on gm_de431.tpc, times 1e9, is 0 or 1 ulp from it for the
// barycentres 1 to 9, Pallas, Nix, the Earth and the Moon, 4 for the Sun and for the sum over 1 to 10 (Linux x64,
// 2026-11-14). The tests print each body's ulp (MEASURE #672): at worst 4 ulp on Linux, 4 on Windows and 5 on macOS
// arm64, the sum over 1 to 10 on each (build-native jobs of CI run 43602458842 at 33b9faf, 2026-11-14; the macOS x64
// job cross-compiles on the arm64 runner and does not run the C++ tests); twice that worst
inline constexpr std::int64_t SpiceParseUlps = 10;

// The GM against the text of gm_de431.tpc, no radii where pck00011.tpc has none, and the built-in body-fixed frame given by
// CSPICE, not through the proxy nor CelestialBody::ReadGM (#672)
static void ExpectBodyReadAsKernels(int id, const char *name, const char *frameName, int frameId)
{
    const auto gm = KernelTextValues("gm_de431.tpc", "BODY" + std::to_string(id) + "_GM");
    ASSERT_EQ(1u, gm.size());
    ASSERT_TRUE(KernelTextValues("pck00011.tpc", "BODY" + std::to_string(id) + "_RADII").empty());
#if !defined(_WIN32)
    SpiceInt frcode{};
    SpiceChar frname[32]{};
    SpiceBoolean frameFound{false};
    cnmfrm_c(name, 32, &frcode, frname, &frameFound);
    ASSERT_TRUE(frameFound);
    ASSERT_STREQ(frameName, frname);
    ASSERT_EQ(frameId, frcode);
#endif

    auto res = GetCelestialBodyInfoProxy(id);
    ASSERT_EQ(id, res.Id);
    ASSERT_STREQ(name, res.Name);
    const auto ulps = UlpDistance(gm[0] * 1E+09, res.GM);
    std::cout << "MEASURE #672 GM of " << id << " from gm_de431.tpc's text, ulp: " << ulps << std::endl;
    ASSERT_LE(ulps, SpiceParseUlps);
    ASSERT_EQ(0.0, res.Radii.x);
    ASSERT_EQ(0.0, res.Radii.y);
    ASSERT_EQ(0.0, res.Radii.z);
    ASSERT_STREQ(frameName, res.FrameName);
    ASSERT_EQ(frameId, res.FrameId);
}

TEST(API, GetBodyInformationGmWithoutRadii)
{
    // A GM in gm_de431.tpc, no RADII in pck00011.tpc: before #672 the failed RADII read left GM 0 and the frame unread
    ExpectBodyReadAsKernels(2000002, "PALLAS", "IAU_PALLAS", 10102);
    ExpectBodyReadAsKernels(902, "NIX", "IAU_NIX", 10108);
}

TEST(API, GetBodyInformationRadiiWithoutGm)
{
    // BODY507_RADII = (40 40 40) km in pck00011.tpc, no BODY507_GM in gm_de431.tpc
    ASSERT_TRUE(KernelTextValues("gm_de431.tpc", "BODY507_GM").empty());
    ASSERT_EQ((std::vector<double>{40.0, 40.0, 40.0}), KernelTextValues("pck00011.tpc", "BODY507_RADII"));
#if !defined(_WIN32)
    SpiceInt frcode{};
    SpiceChar frname[32]{};
    SpiceBoolean frameFound{false};
    cnmfrm_c("ELARA", 32, &frcode, frname, &frameFound);
    ASSERT_TRUE(frameFound);
    ASSERT_STREQ("IAU_ELARA", frname);
    ASSERT_EQ(10029, frcode);
#endif

    auto res = GetCelestialBodyInfoProxy(507);
    ASSERT_EQ(507, res.Id);
    ASSERT_STREQ("ELARA", res.Name);
    ASSERT_EQ(0.0, res.GM);
    ASSERT_EQ(40000.0, res.Radii.x);
    ASSERT_EQ(40000.0, res.Radii.y);
    ASSERT_EQ(40000.0, res.Radii.z);
    ASSERT_STREQ("IAU_ELARA", res.FrameName);
    ASSERT_EQ(10029, res.FrameId);
}

TEST(API, GetBodyInformationSolarSystemBarycenterGm)
{
    // The GM of the solar system barycentre is the sum of the GM of the barycentres 1 to 9 and of the Sun
    double expected{};
    for (int i = 1; i <= 10; ++i)
    {
        const auto gm = KernelTextValues("gm_de431.tpc", "BODY" + std::to_string(i) + "_GM");
        ASSERT_EQ(1u, gm.size());
        expected += gm[0] * 1E+09;
    }

    auto res = GetCelestialBodyInfoProxy(0);
    ASSERT_EQ(0, res.Id);
    ASSERT_STREQ("SOLAR SYSTEM BARYCENTER", res.Name);
    const auto ulps = UlpDistance(expected, res.GM);
    std::cout << "MEASURE #672 GM of 0 from gm_de431.tpc's text (the sum over 1 to 10), ulp: " << ulps << std::endl;
    ASSERT_LE(ulps, SpiceParseUlps);
    ASSERT_STREQ("", res.FrameName);
}

TEST(API, GetBodyInformationBarycenter)
{
#if !defined(_WIN32)
    SpiceInt frcode{};
    SpiceChar frname[32]{};
    SpiceBoolean frameFound{true};
    cnmfrm_c("EARTH BARYCENTER", 32, &frcode, frname, &frameFound);
    ASSERT_FALSE(frameFound);
#endif

    // A named body first, so that the stack holds its frame name
    auto earth = GetCelestialBodyInfoProxy(399);
    ASSERT_STREQ("ITRF93", earth.FrameName);
    auto res = GetCelestialBodyInfoProxy(3);
    ASSERT_EQ(3, res.Id);
    ASSERT_STREQ("EARTH BARYCENTER", res.Name);
    ASSERT_STREQ("", res.FrameName);
    ASSERT_EQ(0, res.FrameId);
}

TEST(API, GetBodyInformationInvalidId)
{
    for (int id: {398, 12345678})
    {
#if !defined(_WIN32)
        SpiceChar name[32]{};
        SpiceBoolean found{true};
        bodc2n_c(id, 32, name, &found);
        ASSERT_FALSE(found);
#endif

        // A named body first, so that the stack holds its name and frame name
        auto earth = GetCelestialBodyInfoProxy(399);
        ASSERT_STREQ("EARTH", earth.Name);
        auto res = GetCelestialBodyInfoProxy(id);
        ASSERT_EQ(-1, res.Id);
        ASSERT_STREQ("", res.Name);
        ASSERT_STREQ("", res.FrameName);
        ASSERT_EQ(0, res.FrameId);
        ASSERT_EQ(0.0, res.GM);
        ASSERT_EQ(0.0, res.Radii.x);
        ASSERT_EQ(0.0, res.Radii.y);
        ASSERT_EQ(0.0, res.Radii.z);
    }
}

TEST(API, TransformFrame)
{
    auto res = TransformFrameProxy(IO::Astrodynamics::Frames::InertialFrames::ICRF().GetName().c_str(), "ITRF93", 0.0);
    ASSERT_NEAR(0.76713121189662548, res.Rotation.w, 1E-09);
    ASSERT_NEAR(-1.8618846012434252e-05, res.Rotation.x, 1E-09);
    ASSERT_NEAR(8.468919252183845e-07, res.Rotation.y, 1E-09);
    ASSERT_NEAR(0.64149022080358797, res.Rotation.z, 1E-09);
    ASSERT_NEAR(-1.9637714059853662e-09, res.AngularVelocity.x, 1E-09);
    ASSERT_NEAR(-2.0389340573814659e-09, res.AngularVelocity.y, 1E-09);
    ASSERT_NEAR(7.2921150642488516e-05, res.AngularVelocity.z, 1E-09);
}

TEST(API, TransformFrameTeme)
{
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::string lines[3]{
        "CZ-3C DEB", "1 39348U 10057N   24238.91466777  .00000306  00000-0  19116-2 0  9995",
        "2 39348  20.0230 212.2863 7218258 312.9449   5.6833  2.25781763 89468"
    };

    IO::Astrodynamics::Time::UTC utc("2024-8-26T22:34:20.00000Z");


    IO::Astrodynamics::OrbitalParameters::TLE tle(earth, lines);
    auto satSvTEME = tle.ToStateVector(utc.ToTDB());
    auto res = TransformFrameProxy(satSvTEME.GetFrame().GetName().c_str(), "J2000",
                                   utc.ToTDB().GetSecondsFromJ2000().count());
    IO::Astrodynamics::Math::Quaternion q(res.Rotation.w, res.Rotation.x, res.Rotation.y, res.Rotation.z);
    IO::Astrodynamics::Math::Vector3D v(res.AngularVelocity.x, res.AngularVelocity.y, res.AngularVelocity.z);

    auto resPosition = satSvTEME.GetPosition().Rotate(q);
    auto resVelocity = satSvTEME.GetVelocity().Rotate(q) - v.CrossProduct(resPosition);

    ASSERT_NEAR(resPosition.GetX(), 32718534.030244593, 1E-06);
    ASSERT_NEAR(resPosition.GetY(), -17501127.803996515, 1E-06);
    ASSERT_NEAR(resPosition.GetZ(), 11592995.3105345, 1E-06);
    ASSERT_NEAR(resVelocity.GetX(), 1808.0437338563306, 1E-06);
    ASSERT_NEAR(resVelocity.GetY(), 998.49491137687698, 1E-06);
    ASSERT_NEAR(resVelocity.GetZ(), 29.876025979417708, 1E-06);
}

TEST(API, ConvertTLEToStateVectorProxy)
{
    IO::Astrodynamics::Time::TDB epoch("2021-01-20T18:50:13.663106");
    auto stateVector = ConvertTLEToStateVectorProxy("ISS",
                                                    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                                                    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703",
                                                    epoch.GetSecondsFromJ2000().count());
    ASSERT_DOUBLE_EQ(4363669.2613373389, stateVector.position.x);
    ASSERT_DOUBLE_EQ(-3627809.912410662, stateVector.position.y);
    ASSERT_DOUBLE_EQ(-3747415.4653566754, stateVector.position.z);
    ASSERT_DOUBLE_EQ(5805.8241824895995, stateVector.velocity.x);
    ASSERT_DOUBLE_EQ(2575.7226437161635, stateVector.velocity.y);
    ASSERT_DOUBLE_EQ(4271.5974622410786, stateVector.velocity.z);
    ASSERT_DOUBLE_EQ(664440682.84760022, stateVector.epoch);
}

TEST(API, GetTLEElementsProxy)
{
    auto res = GetTLEElementsProxy("ISS",
                                   "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                                   "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
    ASSERT_DOUBLE_EQ(6803376.2171725659, res.A);
    ASSERT_DOUBLE_EQ(4.9299999999999999e-05, res.E);
    ASSERT_DOUBLE_EQ(0.9013281683026676, res.I);
    ASSERT_DOUBLE_EQ(6.1615568022666061, res.O);
    ASSERT_DOUBLE_EQ(5.6003339639830649, res.W);
    ASSERT_DOUBLE_EQ(0.68479738531249512, res.M);
    ASSERT_DOUBLE_EQ(664419082.8475914, res.Epoch);
    ASSERT_DOUBLE_EQ(5.06539394194257e-10, res.BalisticCoefficient);
    ASSERT_DOUBLE_EQ(0.0001027, res.DragTerm);
    ASSERT_DOUBLE_EQ(0.0, res.SecondDerivativeOfMeanMotion);
}

TEST(API, ConvertConicOrbitalElementsToStateVector)
{
    double perifocalDist = std::sqrt(std::pow(-6.116559469556896E+06, 2) + std::pow(-1.546174698676721E+06, 2) +
        std::pow(2.521950157430313E+06, 2));

    IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conics;
    conics.SetFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray());
    conics.epoch = 663724800.00001490; //"2021-01-12T11:58:50.816" UTC
    conics.meanAnomaly = 4.541224977546975E+01 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.periapsisArgument = 1.062574316262159E+02 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.ascendingNodeLongitude = 3.257605322534260E+01 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.inclination = 5.171921958517460E+01 * IO::Astrodynamics::Constants::DEG_RAD;
    conics.eccentricity = 1.353139738203394E-03;
    conics.perifocalDistance = perifocalDist;
    conics.centerOfMotionId = 399;

    auto sv = ConvertConicElementsToStateVectorProxy(conics);

    //Low accuracy due to conical propagation
    ASSERT_NEAR(-6.116559469556896E+06, sv.position.x, 3e3);
    ASSERT_NEAR(-1.546174698676721E+06, sv.position.y, 3e3);
    ASSERT_NEAR(2.521950157430313E+06, sv.position.z, 3e3);

    ASSERT_NEAR(-8.078523150700097E+02, sv.velocity.x, 0.2);
    ASSERT_NEAR(-5.477647950892673E+03, sv.velocity.y, 1.2);
    ASSERT_NEAR(-5.297615757935174E+03, sv.velocity.z, 1.1);
    ASSERT_EQ(663724800.00001490, sv.epoch);
    ASSERT_EQ(399, sv.centerOfMotionId);
    ASSERT_STREQ(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray(), sv.inertialFrame);
}


TEST(API, ConvertEquinoctialElementsToStateVector)
{
    //keplerian elements
    double p = 1.0e7;
    double ecc = 0.1;
    double a = p / (1. - ecc);
    double argp = 30.0 * IO::Astrodynamics::Constants::DEG_RAD;
    double node = 15.0 * IO::Astrodynamics::Constants::DEG_RAD;
    double inc = 10.0 * IO::Astrodynamics::Constants::DEG_RAD;
    double m0 = 45.0 * IO::Astrodynamics::Constants::DEG_RAD;
    IO::Astrodynamics::Time::TDB t0{-100000000.0s};

    //equinoctial elements
    double h = ecc * sin(argp + node);
    double k = ecc * cos(argp + node);
    double p2 = tan(inc / 2.) * sin(node);
    double q = tan(inc / 2.) * cos(node);
    double L = m0 + argp + node;

    IO::Astrodynamics::API::DTO::EquinoctialElementsDTO eqDTO;
    eqDTO.SetFrame(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray());
    eqDTO.declinationOfThePole = IO::Astrodynamics::Constants::PI2;
    eqDTO.rightAscensionOfThePole = -IO::Astrodynamics::Constants::PI2;
    eqDTO.ascendingNodeLongitudeRate = 0.0;
    eqDTO.periapsisLongitudeRate = 0.0;
    eqDTO.h = h;
    eqDTO.p = p2;
    eqDTO.semiMajorAxis = a;
    eqDTO.epoch = t0.GetSecondsFromJ2000().count();
    eqDTO.centerOfMotionId = 399;
    eqDTO.L = L;
    eqDTO.k = k;
    eqDTO.q = q;

    auto sv = ConvertEquinoctialElementsToStateVectorProxy(eqDTO);

    ASSERT_DOUBLE_EQ(-1557343.2179623565, sv.position.x);
    ASSERT_DOUBLE_EQ(10112046.56492505, sv.position.y);
    ASSERT_DOUBLE_EQ(1793343.6111546031, sv.position.z);
    ASSERT_DOUBLE_EQ(-6369.0795341145204, sv.velocity.x);
    ASSERT_DOUBLE_EQ(-517.51239201161684, sv.velocity.y);
    ASSERT_DOUBLE_EQ(202.52220483204573, sv.velocity.z);
    ASSERT_EQ(399, sv.centerOfMotionId);
    ASSERT_STREQ(IO::Astrodynamics::Frames::InertialFrames::ICRF().ToCharArray(), sv.inertialFrame);
}

TEST(API, ConvertToRaDec)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    auto moon = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(301, earth);
    auto sv = moon->GetOrbitalParametersAtEpoch()->ToStateVector();

    auto svDTO = ToStateVectorDTO(sv);
    auto ra = ConvertStateVectorToEquatorialCoordinatesProxy(svDTO);
    ASSERT_DOUBLE_EQ(222.44729949955743, ra.rightAscension * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(-10.900186051699617, ra.declination * IO::Astrodynamics::Constants::RAD_DEG);
    ASSERT_DOUBLE_EQ(402448639.88732731, ra.range);
}

TEST(API, ConvertEllipticStateToConic)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    IO::Astrodynamics::API::DTO::StateVectorDTO stateVector;
    stateVector.epoch = 2451545.0; // J2000 epoch
    stateVector.position = {6800000.0, 0.0, 0.0};
    stateVector.velocity = {0.0, 8000.0, 0.0};
    stateVector.centerOfMotionId = earth->GetId();
    stateVector.SetFrame("J2000");

    auto result = ConvertStateVectorToConicOrbitalElementProxy(stateVector, earth->GetMu());

    auto stateVector2 = ConvertConicElementsToStateVectorAtEpochProxy(result, stateVector.epoch, earth->GetMu());
    EXPECT_NEAR(stateVector.position.x, stateVector2.position.x, 1e-6);
    EXPECT_NEAR(stateVector.position.y, stateVector2.position.y, 1e-6);
    EXPECT_NEAR(stateVector.position.z, stateVector2.position.z, 1e-6);
    EXPECT_NEAR(stateVector.velocity.x, stateVector2.velocity.x, 1e-6);
    EXPECT_NEAR(stateVector.velocity.y, stateVector2.velocity.y, 1e-6);
    EXPECT_NEAR(stateVector.velocity.z, stateVector2.velocity.z, 1e-6);
    EXPECT_NEAR(stateVector.epoch, stateVector2.epoch, 1e-6);
}

TEST(API, ConvertHyperbolicStateToConic)
{
    auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    IO::Astrodynamics::API::DTO::StateVectorDTO stateVector;
    stateVector.epoch = 2451545.0; // J2000 epoch
    stateVector.position = {6800000.0, 0.0, 0.0};
    stateVector.velocity = {0.0, 12000.0, 0.0};
    stateVector.centerOfMotionId = earth->GetId();
    stateVector.SetFrame("J2000");
    // double mu = 398600.4418; // Earth's gravitational parameter

    auto result = ConvertStateVectorToConicOrbitalElementProxy(stateVector, earth->GetMu());
    result.centerOfMotionId = 399;
    result.SetFrame("J2000");


    auto stateVector2 = ConvertConicElementsToStateVectorAtEpochProxy(result, stateVector.epoch, earth->GetMu());
    EXPECT_NEAR(stateVector.position.x, stateVector2.position.x, 1e-6);
    EXPECT_NEAR(stateVector.position.y, stateVector2.position.y, 1e-6);
    EXPECT_NEAR(stateVector.position.z, stateVector2.position.z, 1e-6);
    EXPECT_NEAR(stateVector.velocity.x, stateVector2.velocity.x, 1e-6);
    EXPECT_NEAR(stateVector.velocity.y, stateVector2.velocity.y, 1e-6);
    EXPECT_NEAR(stateVector.velocity.z, stateVector2.velocity.z, 1e-6);
    EXPECT_NEAR(stateVector.epoch, stateVector2.epoch, 1e-6);
}

TEST(API, GetFrameNameProxy)
{
    // SPICE's built-in frame table (Frames Required Reading, "Built-in Inertial Reference Frames" and "Built-in PCK-Based
    // IAU Body-Fixed Reference Frames"): J2000 is frame 1, ITRF93 13000 and IAU_EARTH 10013
    char name[33];
    ASSERT_TRUE(GetFrameNameProxy(1, name));
    ASSERT_STREQ("J2000", name);
    ASSERT_TRUE(GetFrameNameProxy(13000, name));
    ASSERT_STREQ("ITRF93", name);
    ASSERT_TRUE(GetFrameNameProxy(10013, name));
    ASSERT_STREQ("IAU_EARTH", name);

    // No frame of that id: false, an empty name and the message
    ASSERT_FALSE(GetFrameNameProxy(-999999000, name));
    ASSERT_STREQ("", name);
    ASSERT_STREQ("SPICE knows no frame of id -999999000", GetLastErrorProxy());
}

namespace
{
    // A TK frame kernel whose frame is rotated by 90 deg about z from J2000: TKFRAME_ANGLES (90, 0, 0) about the axes
    // (3, 2, 1) (NAIF, Frames Required Reading, "Defining a TK Frame Using Euler Angles")
    std::filesystem::path WriteRotatedFrameKernel(const std::string &fileName, int frameId, const std::string &frameName)
    {
        auto file = std::filesystem::temp_directory_path() / fileName;
        std::ofstream out(file);
        auto id = std::to_string(frameId);
        out << "KPL/FK\n\\begindata\n"
            << "FRAME_" << frameName << " = " << id << "\n"
            << "FRAME_" << id << "_NAME = '" << frameName << "'\n"
            << "FRAME_" << id << "_CLASS = 4\n"
            << "FRAME_" << id << "_CLASS_ID = " << id << "\n"
            << "FRAME_" << id << "_CENTER = 399\n"
            << "TKFRAME_" << id << "_RELATIVE = 'J2000'\n"
            << "TKFRAME_" << id << "_SPEC = 'ANGLES'\n"
            << "TKFRAME_" << id << "_UNITS = 'DEGREES'\n"
            << "TKFRAME_" << id << "_AXES = ( 3, 2, 1 )\n"
            << "TKFRAME_" << id << "_ANGLES = ( 90.0, 0.0, 0.0 )\n"
            << "\\begintext\n";
        return file;
    }

    // The rotation angle of the quaternion of the frame to J2000, in degrees (the sign of a quaternion is free)
    double RotationAngleDeg(const IO::Astrodynamics::API::DTO::FrameTransformationDTO &transformation)
    {
        return 2.0 * std::acos(std::min(1.0, std::abs(transformation.Rotation.w))) * 180.0 / IO::Astrodynamics::Constants::PI;
    }
}

TEST(API, UnloadKernelsProxyOfADeletedFile)
{
    // A text kernel is closed once loaded, so it can be deleted on every platform while SPICE still holds its data
    auto file = WriteRotatedFrameKernel("IOA_F02_CPP_A.tf", 1999911, "IOA_F02_CPP_A");
    ASSERT_TRUE(LoadKernelsProxy(file.string().c_str()));
    ASSERT_NEAR(90.0, RotationAngleDeg(TransformFrameProxy("IOA_F02_CPP_A", "J2000", 0.0)), 1e-10);

    std::filesystem::remove(file);
    ASSERT_TRUE(UnloadKernelsProxy(file.string().c_str()));

    // The frame is no longer known: the failed transformation is the zero quaternion (#454)
    auto res = TransformFrameProxy("IOA_F02_CPP_A", "J2000", 0.0);
    ASSERT_EQ(0.0, res.Rotation.w);
    ASSERT_EQ(0.0, res.Rotation.x);
    ASSERT_EQ(0.0, res.Rotation.y);
    ASSERT_EQ(0.0, res.Rotation.z);
}

TEST(API, UnloadKernelsProxyOfANameNotLoaded)
{
    auto file = WriteRotatedFrameKernel("IOA_F02_CPP_B.tf", 1999912, "IOA_F02_CPP_B");
    ASSERT_TRUE(LoadKernelsProxy(file.string().c_str()));
    auto neverLoaded = std::filesystem::temp_directory_path() / "IOA_F02_CPP_never_loaded.tf";
    std::filesystem::remove(neverLoaded);

    // SPICE ignores a name it does not hold, and raises no error
    ASSERT_TRUE(UnloadKernelsProxy(neverLoaded.string().c_str()));
    ASSERT_NEAR(90.0, RotationAngleDeg(TransformFrameProxy("IOA_F02_CPP_B", "J2000", 0.0)), 1e-10);

    ASSERT_TRUE(UnloadKernelsProxy(file.string().c_str()));
    std::filesystem::remove(file);
}

namespace
{
    // A folder of its own under the temporary directory, emptied
    std::filesystem::path FreshFolder(const std::string &name)
    {
        auto folder = std::filesystem::temp_directory_path() / name;
        std::filesystem::remove_all(folder);
        std::filesystem::create_directories(folder);
        return folder;
    }

    // Copies of the L1 SPK of Data/SolarSystem, loaded: more DAFs in use than the 23 logical units of CSPICE's handle
    // manager, so that a later search reconnects the units it released (ZZDDHHLU)
    std::vector<std::filesystem::path> LoadSpkCopies(const std::filesystem::path &folder, int count)
    {
        std::vector<std::filesystem::path> files;
        for (int i = 0; i < count; ++i)
        {
            auto file = folder / ("x" + std::to_string(i) + ".bsp");
            std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L1_de431.bsp", file);
            EXPECT_TRUE(LoadKernelsProxy(file.string().c_str()));
            files.push_back(file);
        }
        return files;
    }

    void UnloadAll(const std::vector<std::filesystem::path> &files)
    {
        for (const auto &file: files)
        {
            UnloadKernelsProxy(file.string().c_str());
        }
    }

    void ExpectSameRotation(const IO::Astrodynamics::API::DTO::FrameTransformationDTO &expected,
                            const IO::Astrodynamics::API::DTO::FrameTransformationDTO &actual)
    {
        EXPECT_EQ(expected.Rotation.w, actual.Rotation.w);
        EXPECT_EQ(expected.Rotation.x, actual.Rotation.x);
        EXPECT_EQ(expected.Rotation.y, actual.Rotation.y);
        EXPECT_EQ(expected.Rotation.z, actual.Rotation.z);
    }
}

// KEEPER's UNLOAD opens again the other loaded kernels of the kind it unloads (unload_c -> UNLOAD -> PCKLOF). Before the
// second round of #668, a PCK deleted since its load failed that reopening (SPICE(FILENOTFOUND)) and stayed open in the DAF
// and PCK tables while KEEPER stored the handle 0 for it: no later unload or kclear_c closed it, and once its logical unit was
// released, every IAU frame failed to reconnect it (SPICE(FILEOPENFAIL), measured with CSPICE N0067). The reference is the
// IAU_EARTH rotation SPICE gives before the scenario, bit for bit
TEST(API, UnloadKernelsProxyWhenAnotherPckIsDeleted)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto before = TransformFrameProxy("J2000", "IAU_EARTH", 0.0);
    ASSERT_NE(0.0, before.Rotation.w);
    auto folder = FreshFolder("ioa_g4_cpp_pck");
    auto a = folder / "a.bpc";
    auto b = folder / "b.bpc";
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/moon_pa_de421_1900-2050.bpc", a);
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/moon_pa_de421_1900-2050.bpc", b);
    ASSERT_TRUE(LoadKernelsProxy(a.string().c_str()));
    ASSERT_TRUE(LoadKernelsProxy(b.string().c_str()));
    std::filesystem::remove(b);

    EXPECT_TRUE(UnloadKernelsProxy(a.string().c_str()));
    auto copies = LoadSpkCopies(folder, 25);
    ExpectSameRotation(before, TransformFrameProxy("J2000", "IAU_EARTH", 0.0));
    EXPECT_TRUE(UnloadKernelsProxy(b.string().c_str()));

    UnloadAll(copies);
    std::filesystem::remove_all(folder);
    ExpectSameRotation(before, TransformFrameProxy("J2000", "IAU_EARTH", 0.0));
#endif
}

// The SPK analog, two deleted SPKs among four: whichever is unloaded first, KEEPER reopens the other one. The reference is
// the state of the Moon from the Earth SPICE gives before the scenario, bit for bit: its segments are in de440s.bsp, loaded
// before the copies, so its search passes every SPK loaded after it (SPKSFS searches the files from the last loaded)
TEST(API, UnloadKernelsProxyOfTwoDeletedSpks)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto before = ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE");
    ASSERT_FALSE(std::isnan(before.position.x));
    auto folder = FreshFolder("ioa_g4_cpp_spk");
    std::vector<std::filesystem::path> files;
    for (const char *name: {"a.bsp", "b.bsp", "c.bsp", "d.bsp"})
    {
        files.push_back(folder / name);
        std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", files.back());
        ASSERT_TRUE(LoadKernelsProxy(files.back().string().c_str()));
    }
    std::filesystem::remove(files[1]);
    std::filesystem::remove(files[2]);

    EXPECT_TRUE(UnloadKernelsProxy(files[0].string().c_str()));
    auto copies = LoadSpkCopies(folder, 25);
    const auto after = ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE");
    EXPECT_EQ(before.position.x, after.position.x);
    EXPECT_EQ(before.position.y, after.position.y);
    EXPECT_EQ(before.position.z, after.position.z);
    EXPECT_EQ(before.velocity.x, after.velocity.x);
    EXPECT_EQ(before.velocity.y, after.velocity.y);
    EXPECT_EQ(before.velocity.z, after.velocity.z);

    UnloadAll(copies);
    UnloadAll(files);
    std::filesystem::remove_all(folder);
#endif
}

#if !defined(_WIN32)
extern "C" int dafhfn_(SpiceInt *handle, char *fname, int fnameLength);

namespace
{
    // The handle KEEPER holds for a file, -1 when KEEPER does not hold it (kdata_c binds to the library's CSPICE on Linux and
    // macOS, where the shared library exports the CSPICE routines it contains)
    SpiceInt KeeperHandle(const std::filesystem::path &file)
    {
        SpiceInt count{};
        ktotal_c("ALL", &count);
        for (SpiceInt i = 0; i < count; ++i)
        {
            SpiceChar name[256]{};
            SpiceChar type[32]{};
            SpiceChar source[256]{};
            SpiceInt handle{};
            SpiceBoolean found{};
            kdata_c(i, "ALL", 256, 32, 256, name, type, source, &handle, &found);
            if (found && file.string() == name)
            {
                return handle;
            }
        }
        return -1;
    }

    // Whether a DAF handle is still open (dafhfn_ signals SPICE(NOSUCHHANDLE) for a closed one; DAF handles are not reused)
    bool DafHandleOpen(SpiceInt handle)
    {
        char name[256]{};
        dafhfn_(&handle, name, sizeof(name));
        if (failed_c())
        {
            reset_c();
            return false;
        }
        return true;
    }
}
#endif

// The native writers unload kernels too: the writer's kernel object unloads the file it writes when it is destroyed, and the
// file it replaces before writing (Kernel::~Kernel, EphemerisKernel::WriteData), and each such unload_c of an SPK opens the
// other loaded SPKs again (unload_c -> UNLOAD -> SPKLEF). Before the second round of #668, those unloads did not first
// close the deleted binary kernels: the reopening failed on a deleted SPK, which stayed open with KEEPER's handle 0,
// and the failure was left pending after the proxy returned true, so that the next unload of an SPK failed. The references
// are KEEPER's list (kdata_c) and the DAF handles (dafhfn_) of the library's CSPICE
TEST(API, WriteEphemerisProxyWhenAnotherSpkIsDeleted)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    auto folder = FreshFolder("ioa_g4_cpp_write_spk");
    auto a = folder / "a.bsp";
    auto b = folder / "b.bsp";
    auto c = folder / "c.bsp";
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", a);
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", b);
    ASSERT_TRUE(LoadKernelsProxy(a.string().c_str()));
    ASSERT_TRUE(LoadKernelsProxy(b.string().c_str()));
    const SpiceInt handle = KeeperHandle(b);
    ASSERT_GT(handle, 0);
    std::filesystem::remove(b);

    IO::Astrodynamics::API::DTO::StateVectorDTO sv[3];
    for (int i = 0; i < 3; ++i)
    {
        sv[i].position = {6800000.0 + i, 0.0, 0.0};
        sv[i].velocity = {0.0, 8000.0, 0.0};
        sv[i].epoch = i;
        sv[i].centerOfMotionId = 399;
        sv[i].SetFrame("J2000");
    }

    // Written twice: the second write replaces the file, which unloads it in WriteData
    EXPECT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999971, sv, 3));
    EXPECT_FALSE(failed_c());
    EXPECT_EQ(-1, KeeperHandle(b));
    EXPECT_FALSE(DafHandleOpen(handle));
    ASSERT_TRUE(LoadKernelsProxy(c.string().c_str()));
    EXPECT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999971, sv, 3));
    EXPECT_FALSE(failed_c());
    EXPECT_TRUE(UnloadKernelsProxy(a.string().c_str()));
    EXPECT_TRUE(UnloadKernelsProxy(c.string().c_str()));
    EXPECT_EQ(-1, KeeperHandle(a));
    EXPECT_EQ(-1, KeeperHandle(c));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// The CK analog: the writer's kernel object unloads the CK it writes when it is destroyed, which opens the other loaded CKs
// again (unload_c -> UNLOAD -> CKLPF). Two CKs of the spacecraft -175 written by the proxy itself, loaded, the second deleted
TEST(API, WriteOrientationProxyWhenAnotherCkIsDeleted)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0),
                                                           IO::Astrodynamics::Math::Vector3D(0.0, 8000.0, 0.0),
                                                           IO::Astrodynamics::Time::TDB(0.0s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-175, "SPC000", 1000.0, 3000.0, std::string(SpacecraftPath),
                                                        std::move(orbitalParams));
    IO::Astrodynamics::API::DTO::StateOrientationDTO so[3];
    for (int i = 0; i < 3; ++i)
    {
        so[i].orientation = {1.0, 0.0, 0.0, 0.0};
        so[i].angularVelocity = {0.0, 0.0, 0.0};
        so[i].epoch = i;
        so[i].SetFrame("J2000");
    }

    auto folder = FreshFolder("ioa_g4_cpp_write_ck");
    auto a = folder / "a.bc";
    auto b = folder / "b.bc";
    auto c = folder / "c.bc";
    ASSERT_TRUE(WriteOrientationProxy(a.string().c_str(), -175, so, 3));
    ASSERT_TRUE(WriteOrientationProxy(b.string().c_str(), -175, so, 3));
    ASSERT_TRUE(LoadKernelsProxy(a.string().c_str()));
    ASSERT_TRUE(LoadKernelsProxy(b.string().c_str()));
    const SpiceInt handle = KeeperHandle(b);
    ASSERT_GT(handle, 0);
    std::filesystem::remove(b);

    EXPECT_TRUE(WriteOrientationProxy(c.string().c_str(), -175, so, 3));
    EXPECT_FALSE(failed_c());
    EXPECT_EQ(-1, KeeperHandle(b));
    EXPECT_FALSE(DafHandleOpen(handle));
    EXPECT_TRUE(UnloadKernelsProxy(a.string().c_str()));
    EXPECT_EQ(-1, KeeperHandle(a));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// A meta-kernel that names its SPK relative to the working directory of its load: SPICE holds the SPK under that relative
// name, and once the working directory changed the name no longer reaches the file, which still exists. Tested by that
// name, the next load of any kernel would take it for deleted, close it and unload it (the first version of the cleanup of
// the second round of #668 did). A relative name is left to SPICE: KEEPER keeps the SPK with its handle open. The references are KEEPER's list and the DAF handles
TEST(API, LoadKernelsProxyAfterTheWorkingDirectoryChangedKeepsAnSpkAMetaKernelNamesRelatively)
{
#if defined(_WIN32)
    GTEST_SKIP() << "KEEPER's list is read through the CSPICE routines the shared library exports on Linux and macOS only";
#else
    auto folder = FreshFolder("ioa_g4_cpp_relative");
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", folder / "x.bsp");
    {
        std::ofstream metaKernel(folder / "mk.tm");
        metaKernel << "KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( 'x.bsp' )\n\\begintext\n";
        std::ofstream text(folder / "t.tk");
        text << "KPL/PCK\n\\begindata\nIOA_G4_RELATIVE = 1\n\\begintext\n";
    }
    const auto workingDirectory = std::filesystem::current_path();
    std::filesystem::current_path(folder);
    const bool loaded = LoadKernelsProxy((folder / "mk.tm").string().c_str());
    std::filesystem::current_path(workingDirectory);
    ASSERT_TRUE(loaded);
    const SpiceInt handle = KeeperHandle("x.bsp");
    ASSERT_GT(handle, 0);

    EXPECT_TRUE(LoadKernelsProxy((folder / "t.tk").string().c_str()));
    EXPECT_EQ(handle, KeeperHandle("x.bsp"));
    EXPECT_TRUE(DafHandleOpen(handle));

    EXPECT_TRUE(UnloadKernelsProxy((folder / "t.tk").string().c_str()));
    EXPECT_TRUE(UnloadKernelsProxy((folder / "mk.tm").string().c_str()));
    EXPECT_EQ(-1, KeeperHandle("x.bsp"));
    EXPECT_FALSE(DafHandleOpen(handle));
    std::filesystem::remove_all(folder);
#endif
}

// A direct C++ caller that loads an SPK by a name relative to the working directory: SPICE holds it under that name, and once
// the working directory changed the name no longer tells whether the file was deleted. The native drop of the deleted binary
// kernels tests only the names SPICE holds as absolute paths, so it leaves this one to SPICE, as it does a meta-kernel's
// relative names (fourth round of #668). The references are KEEPER's list (kdata_c) and the DAF handles (dafhfn_)
TEST(API, LoadKernelsProxyAfterTheWorkingDirectoryChangedKeepsAnSpkLoadedByARelativeName)
{
#if defined(_WIN32)
    GTEST_SKIP() << "KEEPER's list is read through the CSPICE routines the shared library exports on Linux and macOS only";
#else
    auto folder = FreshFolder("ioa_j1_cpp_relative_direct");
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", folder / "y.bsp");
    {
        std::ofstream text(folder / "t.tk");
        text << "KPL/PCK\n\\begindata\nIOA_J1_RELATIVE = 1\n\\begintext\n";
    }
    // The working directory is restored before any other kernel call: the harness loads Data/SolarSystem by relative names,
    // which KEEPER opens again by those names
    const auto workingDirectory = std::filesystem::current_path();
    std::filesystem::current_path(folder);
    const bool loaded = LoadKernelsProxy("y.bsp");
    std::filesystem::current_path(workingDirectory);
    ASSERT_TRUE(loaded);
    const SpiceInt handle = KeeperHandle("y.bsp");
    ASSERT_GT(handle, 0);

    EXPECT_TRUE(LoadKernelsProxy((folder / "t.tk").string().c_str()));
    EXPECT_EQ(handle, KeeperHandle("y.bsp"));
    EXPECT_TRUE(DafHandleOpen(handle));

    EXPECT_TRUE(UnloadKernelsProxy((folder / "t.tk").string().c_str()));
    EXPECT_TRUE(UnloadKernelsProxy("y.bsp"));
    EXPECT_EQ(-1, KeeperHandle("y.bsp"));
    EXPECT_FALSE(DafHandleOpen(handle));
    std::filesystem::remove_all(folder);
#endif
}

#if !defined(_WIN32)
namespace
{
    // Loads the meta-kernel mk.tm of the folder, which names the file relatively, with the working directory set to the
    // folder, restored at once (the harness loads Data/SolarSystem by relative names, which KEEPER opens again by those names)
    bool LoadRelativeMetaKernel(const std::filesystem::path &folder, const std::string &name)
    {
        {
            std::ofstream metaKernel(folder / "mk.tm");
            metaKernel << "KPL/MK\n\\begindata\nKERNELS_TO_LOAD = ( '" << name << "' )\n\\begintext\n";
        }
        const auto workingDirectory = std::filesystem::current_path();
        std::filesystem::current_path(folder);
        const bool loaded = LoadKernelsProxy((folder / "mk.tm").string().c_str());
        std::filesystem::current_path(workingDirectory);
        return loaded;
    }

    void ExpectSameState(const IO::Astrodynamics::API::DTO::StateVectorDTO &expected,
                         const IO::Astrodynamics::API::DTO::StateVectorDTO &actual)
    {
        EXPECT_EQ(expected.position.x, actual.position.x);
        EXPECT_EQ(expected.position.y, actual.position.y);
        EXPECT_EQ(expected.position.z, actual.position.z);
        EXPECT_EQ(expected.velocity.x, actual.velocity.x);
        EXPECT_EQ(expected.velocity.y, actual.velocity.y);
        EXPECT_EQ(expected.velocity.z, actual.velocity.z);
    }
}
#endif

// The writer's kernel object unloads the SPK it wrote when it is destroyed (Kernel::~Kernel), which opens the other loaded
// SPKs again (unload_c -> UNLOAD -> SPKLEF -> DAFOPR). An SPK SPICE holds under a relative name is left to KEEPER by the
// native drop of the deleted kernels, so once its file is deleted that reopening fails (SPICE(FILENOTFOUND)). Before the
// fourth round of #668 the failure came after the proxy's last check: the proxy returned true and left it pending, and the
// next proxy, an unrelated read, failed with it (a NaN state). The proxy now reports it and leaves nothing pending. The
// reference is the state of the Moon from the Earth SPICE gives before the scenario, bit for bit, and failed_c
TEST(API, WriteEphemerisProxyWhenAnSpkHeldUnderARelativeNameIsDeleted)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto before = ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE");
    ASSERT_FALSE(std::isnan(before.position.x));
    auto folder = FreshFolder("ioa_j1_cpp_write_relative_spk");
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", folder / "x.bsp");
    ASSERT_TRUE(LoadRelativeMetaKernel(folder, "x.bsp"));
    const SpiceInt handle = KeeperHandle("x.bsp");
    ASSERT_GT(handle, 0);
    std::filesystem::remove(folder / "x.bsp");

    IO::Astrodynamics::API::DTO::StateVectorDTO sv[3];
    for (int i = 0; i < 3; ++i)
    {
        sv[i].position = {6800000.0 + i, 0.0, 0.0};
        sv[i].velocity = {0.0, 8000.0, 0.0};
        sv[i].epoch = i;
        sv[i].centerOfMotionId = 399;
        sv[i].SetFrame("J2000");
    }

    const bool written = WriteEphemerisProxy((folder / "c.bsp").string().c_str(), -1999981, sv, 3);
    const bool pending = failed_c();
    const std::string message = GetLastErrorProxy();
    reset_c();
    EXPECT_FALSE(written);
    EXPECT_FALSE(pending);
    EXPECT_NE(std::string::npos, message.find("x.bsp")) << message;
    ExpectSameState(before, ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE"));

    // The orphan, still open under the handle the failed reopening left, is closed by that handle
    spkuef_c(handle);
    EXPECT_TRUE(UnloadKernelsProxy((folder / "mk.tm").string().c_str()));
    reset_c();
    ExpectSameState(before, ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE"));
    std::filesystem::remove_all(folder);
#endif
}

// The CK analog: the writer's kernel object unloads the CK it wrote when it is destroyed, which opens the other loaded CKs
// again (unload_c -> UNLOAD -> CKLPF -> DAFOPR), and fails on a deleted CK SPICE holds under a relative name
TEST(API, WriteOrientationProxyWhenACkHeldUnderARelativeNameIsDeleted)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0),
                                                           IO::Astrodynamics::Math::Vector3D(0.0, 8000.0, 0.0),
                                                           IO::Astrodynamics::Time::TDB(0.0s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-175, "SPC000", 1000.0, 3000.0, std::string(SpacecraftPath),
                                                        std::move(orbitalParams));
    IO::Astrodynamics::API::DTO::StateOrientationDTO so[3];
    for (int i = 0; i < 3; ++i)
    {
        so[i].orientation = {1.0, 0.0, 0.0, 0.0};
        so[i].angularVelocity = {0.0, 0.0, 0.0};
        so[i].epoch = i;
        so[i].SetFrame("J2000");
    }

    const auto before = ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE");
    ASSERT_FALSE(std::isnan(before.position.x));
    auto folder = FreshFolder("ioa_j1_cpp_write_relative_ck");
    ASSERT_TRUE(WriteOrientationProxy((folder / "x.bc").string().c_str(), -175, so, 3));
    ASSERT_TRUE(LoadRelativeMetaKernel(folder, "x.bc"));
    const SpiceInt handle = KeeperHandle("x.bc");
    ASSERT_GT(handle, 0);
    std::filesystem::remove(folder / "x.bc");

    const bool written = WriteOrientationProxy((folder / "c.bc").string().c_str(), -175, so, 3);
    const bool pending = failed_c();
    const std::string message = GetLastErrorProxy();
    reset_c();
    EXPECT_FALSE(written);
    EXPECT_FALSE(pending);
    EXPECT_NE(std::string::npos, message.find("x.bc")) << message;
    ExpectSameState(before, ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE"));

    ckupf_c(handle);
    EXPECT_TRUE(UnloadKernelsProxy((folder / "mk.tm").string().c_str()));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

#if !defined(_WIN32)
namespace
{
    std::string FileBytes(const std::filesystem::path &file)
    {
        std::ifstream stream(file, std::ios::binary);
        return {std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>()};
    }
}
#endif

// The writer replaces an existing SPK: its kernel object loads that file, and WriteData unloads it before removing it. That
// unload opens the other loaded SPKs again (unload_c -> UNLOAD -> SPKLEF -> DAFOPR) and fails on a deleted SPK SPICE holds
// under a relative name. Before the review of the fourth round of #668 the writer went on: it removed the file, spkopn_c
// returned at once on the pending error, and the call failed with the old file gone and no new one written. The writer now
// stops before the removal: the old file is left on disk as it was, unloaded (KEEPER's unload closed it before reopening the
// others), and the failure is reported, nothing pending. References: the file's own bytes before the call, KEEPER's list
// (kdata_c), failed_c, and the Moon state SPICE gives before the scenario, bit for bit
TEST(API, WriteEphemerisProxyReplacingAFileWhenAnSpkHeldUnderARelativeNameIsDeleted)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto before = ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE");
    ASSERT_FALSE(std::isnan(before.position.x));
    auto folder = FreshFolder("ioa_j1_cpp_replace_relative_spk");
    IO::Astrodynamics::API::DTO::StateVectorDTO sv[3];
    for (int i = 0; i < 3; ++i)
    {
        sv[i].position = {6800000.0 + i, 0.0, 0.0};
        sv[i].velocity = {0.0, 8000.0, 0.0};
        sv[i].epoch = i;
        sv[i].centerOfMotionId = 399;
        sv[i].SetFrame("J2000");
    }
    const auto c = folder / "c.bsp";
    ASSERT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999982, sv, 3));
    const std::string written = FileBytes(c);
    ASSERT_FALSE(written.empty());

    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", folder / "x.bsp");
    ASSERT_TRUE(LoadRelativeMetaKernel(folder, "x.bsp"));
    const SpiceInt handle = KeeperHandle("x.bsp");
    ASSERT_GT(handle, 0);
    std::filesystem::remove(folder / "x.bsp");

    sv[0].position.x = 6900000.0;
    const bool replaced = WriteEphemerisProxy(c.string().c_str(), -1999982, sv, 3);
    const bool pending = failed_c();
    const std::string message = GetLastErrorProxy();
    reset_c();
    EXPECT_FALSE(replaced);
    EXPECT_FALSE(pending);
    EXPECT_NE(std::string::npos, message.find("x.bsp")) << message;
    ASSERT_TRUE(std::filesystem::exists(c));
    EXPECT_EQ(written, FileBytes(c));
    EXPECT_EQ(-1, KeeperHandle(c));
    ExpectSameState(before, ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE"));

    spkuef_c(handle);
    EXPECT_TRUE(UnloadKernelsProxy((folder / "mk.tm").string().c_str()));
    reset_c();
    ExpectSameState(before, ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE"));
    std::filesystem::remove_all(folder);
#endif
}

// The CK analog: WriteOrientations unloads the CK it replaces (unload_c -> UNLOAD -> CKLPF) before removing it, and that
// unload fails on a deleted CK SPICE holds under a relative name; the old CK is left on disk as it was
TEST(API, WriteOrientationProxyReplacingAFileWhenACkHeldUnderARelativeNameIsDeleted)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0),
                                                           IO::Astrodynamics::Math::Vector3D(0.0, 8000.0, 0.0),
                                                           IO::Astrodynamics::Time::TDB(0.0s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-175, "SPC000", 1000.0, 3000.0, std::string(SpacecraftPath),
                                                        std::move(orbitalParams));
    IO::Astrodynamics::API::DTO::StateOrientationDTO so[3];
    for (int i = 0; i < 3; ++i)
    {
        so[i].orientation = {1.0, 0.0, 0.0, 0.0};
        so[i].angularVelocity = {0.0, 0.0, 0.0};
        so[i].epoch = i;
        so[i].SetFrame("J2000");
    }

    const auto before = ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE");
    ASSERT_FALSE(std::isnan(before.position.x));
    auto folder = FreshFolder("ioa_j1_cpp_replace_relative_ck");
    const auto c = folder / "c.bc";
    ASSERT_TRUE(WriteOrientationProxy(c.string().c_str(), -175, so, 3));
    const std::string written = FileBytes(c);
    ASSERT_FALSE(written.empty());
    ASSERT_TRUE(WriteOrientationProxy((folder / "x.bc").string().c_str(), -175, so, 3));
    ASSERT_TRUE(LoadRelativeMetaKernel(folder, "x.bc"));
    const SpiceInt handle = KeeperHandle("x.bc");
    ASSERT_GT(handle, 0);
    std::filesystem::remove(folder / "x.bc");

    so[0].orientation = {0.0, 1.0, 0.0, 0.0};
    const bool replaced = WriteOrientationProxy(c.string().c_str(), -175, so, 3);
    const bool pending = failed_c();
    const std::string message = GetLastErrorProxy();
    reset_c();
    EXPECT_FALSE(replaced);
    EXPECT_FALSE(pending);
    EXPECT_NE(std::string::npos, message.find("x.bc")) << message;
    ASSERT_TRUE(std::filesystem::exists(c));
    EXPECT_EQ(written, FileBytes(c));
    EXPECT_EQ(-1, KeeperHandle(c));
    ExpectSameState(before, ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE"));

    ckupf_c(handle);
    EXPECT_TRUE(UnloadKernelsProxy((folder / "mk.tm").string().c_str()));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// #706: the native writers and the kernel loaders let a C++ exception cross P/Invoke, which ended the host process
// (std::terminate, exit code 134). The rule of the native writer itself is the reference (EphemerisKernel::WriteData: three
// states or more, in one frame), with POSIX's ELOOP for a symbolic-link loop, and KEEPER's list (kdata_c) for the kernels a
// writer leaves loaded
namespace
{
    // Equally spaced states of a circular orbit about the Earth in J2000, from the epoch, x offset by dx
    std::vector<IO::Astrodynamics::API::DTO::StateVectorDTO> EarthStates(int count, double dx = 0.0)
    {
        std::vector<IO::Astrodynamics::API::DTO::StateVectorDTO> sv(count);
        for (int i = 0; i < count; ++i)
        {
            sv[i].position = {6800000.0 + dx + i, 0.0, 0.0};
            sv[i].velocity = {0.0, 8000.0, 0.0};
            sv[i].epoch = i;
            sv[i].centerOfMotionId = 399;
            sv[i].SetFrame("J2000");
        }
        return sv;
    }

#if !defined(_WIN32)
    // Whether SPICE has an error pending, which is then reset (failed_c and reset_c bind to the library's CSPICE on Linux and
    // macOS, where the shared library exports the CSPICE routines it contains)
    bool TakePendingSpiceError()
    {
        const bool pending = failed_c();
        reset_c();
        return pending;
    }
#else
    // The Windows DLL exports no CSPICE routine, so the pending error is checked on Linux and macOS only; the proxy's result,
    // its message and the file are checked on every platform
    bool TakePendingSpiceError()
    {
        return false;
    }
#endif

    // Calls the writer of an SPK and expects it to return false without an exception, nothing pending, nothing written, and
    // the message to contain the given text
    void ExpectEphemerisRejected(std::vector<IO::Astrodynamics::API::DTO::StateVectorDTO> sv, const std::string &name,
                                 const std::string &text)
    {
        auto folder = FreshFolder(name);
        const auto file = folder / "e.bsp";
        bool written = true;
        EXPECT_NO_THROW(written = WriteEphemerisProxy(file.string().c_str(), -1999701, sv.data(),
                                                      static_cast<unsigned int>(sv.size())));
        const std::string message = GetLastErrorProxy();
        const bool pending = TakePendingSpiceError();
        EXPECT_FALSE(written);
        EXPECT_FALSE(pending);
        EXPECT_NE(std::string::npos, message.find(text)) << message;
        EXPECT_FALSE(std::filesystem::exists(file));
        std::filesystem::remove_all(folder);
    }

    std::vector<IO::Astrodynamics::API::DTO::StateOrientationDTO> Records(int count)
    {
        std::vector<IO::Astrodynamics::API::DTO::StateOrientationDTO> so(std::max(count, 1));
        for (int i = 0; i < count; ++i)
        {
            so[i].orientation = {1.0, 0.0, 0.0, 0.0};
            so[i].angularVelocity = {0.0, 0.0, 0.0};
            so[i].epoch = i;
            so[i].SetFrame("J2000");
        }
        return so;
    }

    void ExpectOrientationRejected(std::vector<IO::Astrodynamics::API::DTO::StateOrientationDTO> so, unsigned int size,
                                   const std::string &name, const std::string &text)
    {
        auto folder = FreshFolder(name);
        const auto file = folder / "o.bc";
        bool written = true;
        EXPECT_NO_THROW(written = WriteOrientationProxy(file.string().c_str(), -175, so.data(), size));
        const std::string message = GetLastErrorProxy();
        const bool pending = TakePendingSpiceError();
        EXPECT_FALSE(written);
        EXPECT_FALSE(pending);
        EXPECT_NE(std::string::npos, message.find(text)) << message;
        EXPECT_FALSE(std::filesystem::exists(file));
        std::filesystem::remove_all(folder);
    }
}

TEST(API, WriteEphemerisProxyOfOneStateReturnsFalse)
{
    ExpectEphemerisRejected(EarthStates(1), "ioa_k1_cpp_one_state", "State vector set must have 3 items or more");
}

TEST(API, WriteEphemerisProxyOfTwoStatesReturnsFalse)
{
    ExpectEphemerisRejected(EarthStates(2), "ioa_k1_cpp_two_states", "State vector set must have 3 items or more");
}

TEST(API, WriteEphemerisProxyOfTwoFramesReturnsFalse)
{
    auto sv = EarthStates(3);
    sv[2].SetFrame("ECLIPJ2000");
    ExpectEphemerisRejected(sv, "ioa_k1_cpp_two_frames", "State vectors must have the same frame");
}

TEST(API, WriteOrientationProxyOfNoRecordReturnsFalse)
{
    ExpectOrientationRejected(Records(0), 0, "ioa_k1_cpp_no_record", "Orientation array is empty");
}

TEST(API, WriteOrientationProxyOfTwoFramesReturnsFalse)
{
    auto so = Records(3);
    so[2].SetFrame("ECLIPJ2000");
    ExpectOrientationRejected(so, 3, "ioa_k1_cpp_two_frames_ck", "All orientations must have the same frame");
}

// A path that is a loop of symbolic links: the throwing std::filesystem calls of the kernel objects (exists) and of
// KernelsLoader (is_regular_file) raise filesystem_error with POSIX's ELOOP, which crossed P/Invoke. Each proxy now returns
// false with the message, nothing pending, nothing loaded; the unload, which tests the path without throwing, still succeeds
TEST(API, KernelProxiesOfASymbolicLinkLoopReturnFalse)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A symbolic link needs a privilege on Windows";
#else
    auto folder = FreshFolder("ioa_k1_cpp_loop");
    const auto a = folder / "a.bsp";
    const auto b = folder / "b.bsp";
    std::filesystem::create_symlink(b, a);
    std::filesystem::create_symlink(a, b);
    auto sv = EarthStates(3);
    auto so = Records(3);

    const auto expectRejected = [&](const std::function<bool()> &call, const char *what)
    {
        bool result = true;
        EXPECT_NO_THROW(result = call()) << what;
        const bool pending = failed_c();
        const std::string message = GetLastErrorProxy();
        reset_c();
        EXPECT_FALSE(result) << what;
        EXPECT_FALSE(pending) << what;
        EXPECT_NE(std::string::npos, message.find("symbolic links")) << what << ": " << message;
        EXPECT_EQ(-1, KeeperHandle(a)) << what;
    };
    expectRejected([&] { return WriteEphemerisProxy(a.string().c_str(), -1999702, sv.data(), 3); }, "WriteEphemerisProxy");
    expectRejected([&] { return WriteOrientationProxy(a.string().c_str(), -175, so.data(), 3); }, "WriteOrientationProxy");
    expectRejected([&] { return LoadKernelsProxy(a.string().c_str()); }, "LoadKernelsProxy");
    EXPECT_TRUE(UnloadKernelsProxy(a.string().c_str()));
    EXPECT_FALSE(failed_c());
    std::filesystem::remove_all(folder);
#endif
}

// A file the writer replaces, loaded twice before: furnsh_c of a file already loaded adds a second entry of the same handle
// to KEEPER, and each unload_c removes one. The writer's kernel object loads it once more, then WriteData unloaded it once:
// the loads before were left in KEEPER, which went on holding the replaced file, deleted but open, and reading its data
// (measured on c533a6e). The writer now unloads every load of the name, so the written file is no longer loaded and, loaded
// again, gives the new states. References: KEEPER's list (kdata_c) and the states written
TEST(API, WriteEphemerisProxyReplacingAFileLoadedTwiceUnloadsEveryLoad)
{
#if defined(_WIN32)
    GTEST_SKIP() << "KEEPER's list is read through the CSPICE routines the shared library exports on Linux and macOS only";
#else
    auto folder = FreshFolder("ioa_k1_cpp_loaded_twice");
    const auto c = folder / "c.bsp";
    auto first = EarthStates(3);
    ASSERT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999703, first.data(), 3));
    furnsh_c(c.string().c_str());
    furnsh_c(c.string().c_str());
    ASSERT_FALSE(failed_c());
    ASSERT_GT(KeeperHandle(c), 0);

    auto second = EarthStates(5, 5000.0);
    EXPECT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999703, second.data(), 5));
    EXPECT_FALSE(failed_c());
    EXPECT_EQ(-1, KeeperHandle(c));

    ASSERT_TRUE(LoadKernelsProxy(c.string().c_str()));
    const auto read = ReadEphemerisAtGivenEpochProxy(4.0, 399, -1999703, "J2000", "NONE");
    EXPECT_DOUBLE_EQ(second[4].position.x, read.position.x);
    for (int i = 0; i < 4 && KeeperHandle(c) != -1; ++i)
    {
        UnloadKernelsProxy(c.string().c_str());
    }
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// The CK analog: OrientationKernel::WriteOrientations unloads every load of the CK it replaces
TEST(API, WriteOrientationProxyReplacingAFileLoadedTwiceUnloadsEveryLoad)
{
#if defined(_WIN32)
    GTEST_SKIP() << "KEEPER's list is read through the CSPICE routines the shared library exports on Linux and macOS only";
#else
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0),
                                                           IO::Astrodynamics::Math::Vector3D(0.0, 8000.0, 0.0),
                                                           IO::Astrodynamics::Time::TDB(0.0s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-175, "SPC000", 1000.0, 3000.0, std::string(SpacecraftPath),
                                                        std::move(orbitalParams));
    auto folder = FreshFolder("ioa_k1_cpp_ck_loaded_twice");
    const auto c = folder / "c.bc";
    auto so = Records(3);
    ASSERT_TRUE(WriteOrientationProxy(c.string().c_str(), -175, so.data(), 3));
    furnsh_c(c.string().c_str());
    furnsh_c(c.string().c_str());
    ASSERT_FALSE(failed_c());
    ASSERT_GT(KeeperHandle(c), 0);

    auto more = Records(5);
    EXPECT_TRUE(WriteOrientationProxy(c.string().c_str(), -175, more.data(), 5));
    EXPECT_FALSE(failed_c());
    EXPECT_EQ(-1, KeeperHandle(c));
    for (int i = 0; i < 4 && KeeperHandle(c) != -1; ++i)
    {
        UnloadKernelsProxy(c.string().c_str());
    }
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// UnloadKernelsProxy dropped SPICE's message of a failed unload: it reset SPICE without copying the message to lastError, so
// the .NET layer could not name the cause. The unload of an SPK opens the other loaded SPKs again (unload_c -> UNLOAD ->
// SPKLEF) and fails on a deleted SPK SPICE holds under a relative name (SPICE(FILENOTFOUND)). Reference: that message, which
// names the deleted file
TEST(API, UnloadKernelsProxyKeepsTheMessageOfAFailedUnload)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A loaded binary kernel cannot be deleted on Windows";
#else
    const auto before = ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE");
    ASSERT_FALSE(std::isnan(before.position.x));
    auto folder = FreshFolder("ioa_k1_cpp_unload_message");
    const auto c = folder / "c.bsp";
    auto sv = EarthStates(3);
    ASSERT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999704, sv.data(), 3));
    ASSERT_TRUE(LoadKernelsProxy(c.string().c_str()));
    std::filesystem::copy_file(std::string(SolarSystemKernelPath) + "/L2_de431.bsp", folder / "x.bsp");
    ASSERT_TRUE(LoadRelativeMetaKernel(folder, "x.bsp"));
    const SpiceInt handle = KeeperHandle("x.bsp");
    ASSERT_GT(handle, 0);
    std::filesystem::remove(folder / "x.bsp");

    const bool unloaded = UnloadKernelsProxy(c.string().c_str());
    const bool pending = failed_c();
    const std::string message = GetLastErrorProxy();
    reset_c();
    EXPECT_FALSE(unloaded);
    EXPECT_FALSE(pending);
    EXPECT_NE(std::string::npos, message.find("x.bsp")) << message;

    spkuef_c(handle);
    EXPECT_TRUE(UnloadKernelsProxy((folder / "mk.tm").string().c_str()));
    reset_c();
    ExpectSameState(before, ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE"));
    std::filesystem::remove_all(folder);
#endif
}

// A file the writer replaces, loaded under the name of a symbolic link to it, and of a link to that link: KEEPER held the
// replaced file through each name, deleted but open, and SPICE went on reading its data once the link named the new file
// (review of #706, measured on 9338a40: KEEPER still held both links). The writer now unloads every name whose symbolic
// links resolve through the file it replaces. References: KEEPER's list (kdata_c) and the states written
TEST(API, WriteEphemerisProxyReplacingAFileLoadedThroughSymbolicLinksUnloadsThem)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A symbolic link needs a privilege on Windows";
#else
    auto folder = FreshFolder("ioa_k1r_cpp_link_spk");
    const auto c = folder / "c.bsp";
    const auto link = folder / "link.bsp";
    const auto chain = folder / "chain.bsp";
    auto first = EarthStates(3);
    ASSERT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999731, first.data(), 3));
    std::filesystem::create_symlink(c, link);
    std::filesystem::create_symlink(link, chain);
    ASSERT_TRUE(LoadKernelsProxy(link.string().c_str()));
    ASSERT_TRUE(LoadKernelsProxy(chain.string().c_str()));
    ASSERT_GT(KeeperHandle(link), 0);
    ASSERT_GT(KeeperHandle(chain), 0);

    auto second = EarthStates(5, 5000.0);
    EXPECT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999731, second.data(), 5));
    EXPECT_FALSE(failed_c());
    EXPECT_EQ(-1, KeeperHandle(link));
    EXPECT_EQ(-1, KeeperHandle(chain));

    ASSERT_TRUE(LoadKernelsProxy(link.string().c_str()));
    const auto read = ReadEphemerisAtGivenEpochProxy(4.0, 399, -1999731, "J2000", "NONE");
    EXPECT_DOUBLE_EQ(second[4].position.x, read.position.x);
    EXPECT_TRUE(UnloadKernelsProxy(link.string().c_str()));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// The writer given a symbolic link replaces the link, not the file it names: the file stays loaded, its data unchanged,
// and a name that resolves through the link (a link to the link) is unloaded, since it now names the new file. A hard link
// to a replaced file still names the replaced data, which the replacement leaves on disk: it stays loaded too. References:
// KEEPER's list and the states written
TEST(API, WriteEphemerisProxyReplacingALinkKeepsTheFilesThatDoNotResolveThroughIt)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A symbolic link needs a privilege on Windows";
#else
    auto folder = FreshFolder("ioa_k1r_cpp_link_kept");
    const auto c = folder / "c.bsp";
    const auto link = folder / "link.bsp";
    const auto chain = folder / "chain.bsp";
    const auto d = folder / "d.bsp";
    const auto hard = folder / "hard.bsp";
    auto first = EarthStates(3);
    ASSERT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999732, first.data(), 3));
    ASSERT_TRUE(WriteEphemerisProxy(d.string().c_str(), -1999733, first.data(), 3));
    std::filesystem::create_symlink(c, link);
    std::filesystem::create_symlink(link, chain);
    std::filesystem::create_hard_link(d, hard);
    ASSERT_TRUE(LoadKernelsProxy(c.string().c_str()));
    ASSERT_TRUE(LoadKernelsProxy(chain.string().c_str()));
    ASSERT_TRUE(LoadKernelsProxy(hard.string().c_str()));

    auto second = EarthStates(5, 5000.0);
    EXPECT_TRUE(WriteEphemerisProxy(link.string().c_str(), -1999732, second.data(), 5));
    EXPECT_TRUE(WriteEphemerisProxy(d.string().c_str(), -1999733, second.data(), 5));
    EXPECT_FALSE(failed_c());
    EXPECT_GT(KeeperHandle(c), 0);
    EXPECT_EQ(-1, KeeperHandle(chain));
    EXPECT_GT(KeeperHandle(hard), 0);
    EXPECT_FALSE(std::filesystem::is_symlink(link));
    EXPECT_DOUBLE_EQ(first[1].position.x, ReadEphemerisAtGivenEpochProxy(1.0, 399, -1999732, "J2000", "NONE").position.x);
    EXPECT_DOUBLE_EQ(first[1].position.x, ReadEphemerisAtGivenEpochProxy(1.0, 399, -1999733, "J2000", "NONE").position.x);
    EXPECT_TRUE(UnloadKernelsProxy(c.string().c_str()));
    EXPECT_TRUE(UnloadKernelsProxy(hard.string().c_str()));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// The CK analog of WriteEphemerisProxyReplacingAFileLoadedThroughSymbolicLinksUnloadsThem
TEST(API, WriteOrientationProxyReplacingAFileLoadedThroughASymbolicLinkUnloadsIt)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A symbolic link needs a privilege on Windows";
#else
    const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
    std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParams = std::make_unique<
        IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0),
                                                           IO::Astrodynamics::Math::Vector3D(0.0, 8000.0, 0.0),
                                                           IO::Astrodynamics::Time::TDB(0.0s),
                                                           IO::Astrodynamics::Frames::InertialFrames::ICRF());
    IO::Astrodynamics::Body::Spacecraft::Spacecraft spc(-175, "SPC000", 1000.0, 3000.0, std::string(SpacecraftPath),
                                                        std::move(orbitalParams));
    auto folder = FreshFolder("ioa_k1r_cpp_link_ck");
    const auto c = folder / "c.bc";
    const auto link = folder / "link.bc";
    auto so = Records(3);
    ASSERT_TRUE(WriteOrientationProxy(c.string().c_str(), -175, so.data(), 3));
    std::filesystem::create_symlink(c, link);
    ASSERT_TRUE(LoadKernelsProxy(link.string().c_str()));
    ASSERT_GT(KeeperHandle(link), 0);

    auto more = Records(5);
    EXPECT_TRUE(WriteOrientationProxy(c.string().c_str(), -175, more.data(), 5));
    EXPECT_FALSE(failed_c());
    EXPECT_EQ(-1, KeeperHandle(link));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// The proxy of the managed writer of SPKs (SpiceAPI.WriteEphemerisArcs), which made one unload_c of the file it replaces:
// a file loaded twice, and under a symbolic link, stayed in KEEPER (review of #706, measured on 9338a40 through
// Spacecraft.WriteEphemeris). It unloads every load that resolves through the file; another file stays loaded
TEST(API, UnloadEveryLoadProxyUnloadsEveryNameThatResolvesThroughTheFile)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A symbolic link needs a privilege on Windows";
#else
    auto folder = FreshFolder("ioa_k1r_cpp_every_load");
    const auto c = folder / "c.bsp";
    const auto d = folder / "d.bsp";
    const auto link = folder / "link.bsp";
    auto sv = EarthStates(3);
    ASSERT_TRUE(WriteEphemerisProxy(c.string().c_str(), -1999734, sv.data(), 3));
    ASSERT_TRUE(WriteEphemerisProxy(d.string().c_str(), -1999735, sv.data(), 3));
    std::filesystem::create_symlink(c, link);
    furnsh_c(c.string().c_str());
    furnsh_c(c.string().c_str());
    furnsh_c(link.string().c_str());
    furnsh_c(d.string().c_str());
    ASSERT_FALSE(failed_c());

    EXPECT_TRUE(UnloadEveryLoadProxy(c.string().c_str()));
    EXPECT_FALSE(failed_c());
    EXPECT_EQ(-1, KeeperHandle(c));
    EXPECT_EQ(-1, KeeperHandle(link));
    EXPECT_GT(KeeperHandle(d), 0);
    EXPECT_TRUE(UnloadKernelsProxy(d.string().c_str()));
    reset_c();
    std::filesystem::remove_all(folder);
#endif
}

// The rule both the native and the .NET layers apply to the names of the loaded kernels: a name resolves through a file
// when it is the same string, or an absolute name whose symbolic links, followed one at a time, reach the file's entry in
// its directory (its directory's links resolved, its own name kept). A hard link, the target of a link given as the file,
// a relative name and a name that does not exist do not
TEST(API, KernelNameResolvesThroughProxy)
{
#if defined(_WIN32)
    GTEST_SKIP() << "A symbolic link needs a privilege on Windows";
#else
    auto folder = FreshFolder("ioa_k1r_cpp_resolves");
    const auto c = folder / "c.bsp";
    const auto link = folder / "link.bsp";
    const auto chain = folder / "chain.bsp";
    const auto hard = folder / "hard.bsp";
    const auto linkedFolder = folder / "linked";
    const auto real = folder / "real";
    std::filesystem::create_directories(real);
    std::ofstream(c.string()) << "c";
    std::ofstream((real / "e.bsp").string()) << "e";
    std::filesystem::create_symlink(c, link);
    std::filesystem::create_symlink(link, chain);
    std::filesystem::create_hard_link(c, hard);
    std::filesystem::create_directory_symlink(real, linkedFolder);

    EXPECT_TRUE(KernelNameResolvesThroughProxy(c.string().c_str(), c.string().c_str()));
    EXPECT_TRUE(KernelNameResolvesThroughProxy(link.string().c_str(), c.string().c_str()));
    EXPECT_TRUE(KernelNameResolvesThroughProxy(chain.string().c_str(), c.string().c_str()));
    EXPECT_TRUE(KernelNameResolvesThroughProxy(chain.string().c_str(), link.string().c_str()));
    EXPECT_TRUE(KernelNameResolvesThroughProxy((linkedFolder / "e.bsp").string().c_str(), (real / "e.bsp").string().c_str()));
    EXPECT_TRUE(KernelNameResolvesThroughProxy((real / "e.bsp").string().c_str(), (linkedFolder / "e.bsp").string().c_str()));
    EXPECT_FALSE(KernelNameResolvesThroughProxy(c.string().c_str(), link.string().c_str()));
    EXPECT_FALSE(KernelNameResolvesThroughProxy(hard.string().c_str(), c.string().c_str()));
    EXPECT_FALSE(KernelNameResolvesThroughProxy("c.bsp", c.string().c_str()));
    EXPECT_TRUE(KernelNameResolvesThroughProxy("c.bsp", "c.bsp"));
    EXPECT_FALSE(KernelNameResolvesThroughProxy((folder / "missing.bsp").string().c_str(), c.string().c_str()));
    EXPECT_FALSE(failed_c());
    std::filesystem::remove_all(folder);
#endif
}

// #711 and #712: no C++ exception crosses P/Invoke from the element conversions, the TLE proxies and the body information,
// and no proxy that fails leaves SPICE failed for the next call. A centre of motion the native layer reads as a celestial
// body (CelestialBody(int)) threw SDKException out of the proxy when SPICE names no body of its id (exit code 134 of the .NET
// host), and, when the kernel pool has no GM for it, read the found flag and the name bodc2n_c leaves unwritten after the
// failed GM read (CSPICE bodc2n.c returns at once on a pending error). The proxies now return a NaN result with the message
// of the exception then SPICE's, SPICE reset. References: SPICE's names (bodc2n_c through GetCelestialBodyInfoProxy, whose
// record has the Id -1 for an id SPICE does not name, #672), the text of gm_de431.tpc (no BODY507_GM) and SPICE's messages
namespace
{
    // A text kernel of the given data under the temporary directory, written and loaded
    std::filesystem::path LoadTextKernel(const std::string &fileName, const std::string &data)
    {
        auto file = std::filesystem::temp_directory_path() / fileName;
        std::ofstream(file.string()) << "KPL/PCK\n\\begindata\n" << data << "\n\\begintext\n";
        EXPECT_TRUE(LoadKernelsProxy(file.string().c_str())) << GetLastErrorProxy();
        return file;
    }

    void UnloadTextKernel(const std::filesystem::path &file)
    {
        EXPECT_TRUE(UnloadKernelsProxy(file.string().c_str())) << GetLastErrorProxy();
        std::filesystem::remove(file);
    }

    // The message of the last proxy, nothing pending, and the next proxy succeeds: J2000 is SPICE's built-in frame 1
    void ExpectFailureReported(const std::string &text)
    {
        const std::string message = GetLastErrorProxy();
        const bool pending = TakePendingSpiceError();
        EXPECT_FALSE(pending);
        EXPECT_NE(std::string::npos, message.find(text)) << message;
        char name[33];
        EXPECT_TRUE(GetFrameNameProxy(1, name)) << GetLastErrorProxy();
        EXPECT_STREQ("J2000", name);
    }

    void ExpectNaN(const IO::Astrodynamics::API::DTO::StateVectorDTO &sv)
    {
        EXPECT_TRUE(std::isnan(sv.position.x));
        EXPECT_TRUE(std::isnan(sv.position.y));
        EXPECT_TRUE(std::isnan(sv.position.z));
        EXPECT_TRUE(std::isnan(sv.velocity.x));
        EXPECT_TRUE(std::isnan(sv.velocity.y));
        EXPECT_TRUE(std::isnan(sv.velocity.z));
    }

    IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO ConicsAbout(int centre)
    {
        IO::Astrodynamics::API::DTO::ConicOrbitalElementsDTO conics;
        conics.SetFrame("J2000");
        conics.epoch = 0.0;
        conics.perifocalDistance = 7.0e6;
        conics.eccentricity = 0.01;
        conics.inclination = 0.2;
        conics.ascendingNodeLongitude = 0.3;
        conics.periapsisArgument = 0.4;
        conics.meanAnomaly = 0.5;
        conics.centerOfMotionId = centre;
        return conics;
    }

    IO::Astrodynamics::API::DTO::EquinoctialElementsDTO EquinoctialAbout(int centre)
    {
        IO::Astrodynamics::API::DTO::EquinoctialElementsDTO eq;
        eq.SetFrame("J2000");
        eq.epoch = 0.0;
        eq.semiMajorAxis = 7.0e6;
        eq.h = 0.01;
        eq.k = 0.0;
        eq.p = 0.1;
        eq.q = 0.0;
        eq.L = 0.5;
        eq.periapsisLongitudeRate = 0.0;
        eq.ascendingNodeLongitudeRate = 0.0;
        eq.rightAscensionOfThePole = -IO::Astrodynamics::Constants::PI2;
        eq.declinationOfThePole = IO::Astrodynamics::Constants::PI2;
        eq.centerOfMotionId = centre;
        return eq;
    }

    // An identifier SPICE does not name, with a GM in the kernel pool
    constexpr int UnnamedCentre = -1999813;

    void ExpectUnnamed(int id)
    {
#if !defined(_WIN32)
        SpiceChar name[32]{};
        SpiceBoolean found{SPICETRUE};
        bodc2n_c(id, 32, name, &found);
        EXPECT_FALSE(found);
#endif
        EXPECT_EQ(-1, GetCelestialBodyInfoProxy(id).Id);
    }
}

TEST(API, ConvertConicElementsToStateVectorProxyOfAnUnnamedCentreWithAGmReturnsNaN)
{
    ExpectUnnamed(UnnamedCentre);
    auto kernel = LoadTextKernel("ioa_711_cpp_gm.tpc", "BODY-1999813_GM = ( 1.0E-10 )");
    IO::Astrodynamics::API::DTO::StateVectorDTO sv;
    EXPECT_NO_THROW(sv = ConvertConicElementsToStateVectorProxy(ConicsAbout(UnnamedCentre)));
    ExpectFailureReported("can't be found");
    ExpectNaN(sv);
    EXPECT_EQ(UnnamedCentre, sv.centerOfMotionId);
    UnloadTextKernel(kernel);
}

TEST(API, ConvertEquinoctialElementsToStateVectorProxyOfAnUnnamedCentreWithAGmReturnsNaN)
{
    ExpectUnnamed(UnnamedCentre);
    auto kernel = LoadTextKernel("ioa_711_cpp_gm_eq.tpc", "BODY-1999813_GM = ( 1.0E-10 )");
    IO::Astrodynamics::API::DTO::StateVectorDTO sv;
    EXPECT_NO_THROW(sv = ConvertEquinoctialElementsToStateVectorProxy(EquinoctialAbout(UnnamedCentre)));
    ExpectFailureReported("can't be found");
    ExpectNaN(sv);
    EXPECT_EQ(UnnamedCentre, sv.centerOfMotionId);
    UnloadTextKernel(kernel);
}

TEST(API, ConvertStateVectorToEquatorialCoordinatesProxyOfAnUnnamedCentreWithAGmReturnsNaN)
{
    ExpectUnnamed(UnnamedCentre);
    auto kernel = LoadTextKernel("ioa_711_cpp_gm_radec.tpc", "BODY-1999813_GM = ( 1.0E-10 )");
    IO::Astrodynamics::API::DTO::StateVectorDTO sv;
    sv.position = {7.0e6, 0.0, 0.0};
    sv.velocity = {0.0, 7.5e3, 0.0};
    sv.epoch = 0.0;
    sv.centerOfMotionId = UnnamedCentre;
    sv.SetFrame("J2000");
    IO::Astrodynamics::API::DTO::RaDecDTO raDec;
    EXPECT_NO_THROW(raDec = ConvertStateVectorToEquatorialCoordinatesProxy(sv));
    ExpectFailureReported("can't be found");
    EXPECT_TRUE(std::isnan(raDec.rightAscension));
    EXPECT_TRUE(std::isnan(raDec.declination));
    EXPECT_TRUE(std::isnan(raDec.range));
    UnloadTextKernel(kernel);
}

// ELARA (507): named by SPICE, BODY507_RADII in pck00011.tpc, no BODY507_GM in gm_de431.tpc. Before, both conversions
// returned the zero state, silently (measured from .NET on 46e6975), the found flag of the name read undefined
TEST(API, ConvertConicElementsToStateVectorProxyOfACentreWithoutGmReturnsNaN)
{
    ASSERT_TRUE(KernelTextValues("gm_de431.tpc", "BODY507_GM").empty());
    ASSERT_EQ(507, GetCelestialBodyInfoProxy(507).Id);
    IO::Astrodynamics::API::DTO::StateVectorDTO sv;
    EXPECT_NO_THROW(sv = ConvertConicElementsToStateVectorProxy(ConicsAbout(507)));
    ExpectFailureReported("BODY507_GM");
    ExpectNaN(sv);
}

TEST(API, ConvertEquinoctialElementsToStateVectorProxyOfACentreWithoutGmReturnsNaN)
{
    ASSERT_TRUE(KernelTextValues("gm_de431.tpc", "BODY507_GM").empty());
    IO::Astrodynamics::API::DTO::StateVectorDTO sv;
    EXPECT_NO_THROW(sv = ConvertEquinoctialElementsToStateVectorProxy(EquinoctialAbout(507)));
    ExpectFailureReported("BODY507_GM");
    ExpectNaN(sv);
}

// The 33334 set of Data/Sgp4Reference (checksums recomputed): Vallado's SGP4 error 3 and CSPICE's BADPECCENTRICITY at its
// epoch (tools/sgp4_reference). Before, the state proxy returned the zero state, its SPICE message dropped
static const char *const FailedSgp4Line1 = "1 33334U 78066F   06174.85818871  .00000620  00000-0  10000-3 0  6806";
static const char *const FailedSgp4Line2 = "2 33334  68.4714 236.1303 5602877 123.7484 302.5767  0.00001000 67521";

TEST(API, ConvertTLEToStateVectorProxyOfAFailedSgp4ReturnsNaN)
{
    IO::Astrodynamics::API::DTO::StateVectorDTO sv;
    EXPECT_NO_THROW(sv = ConvertTLEToStateVectorProxy("ERR", FailedSgp4Line1, FailedSgp4Line2, 2.0e8));
    // Taken before ExpectFailureReported, whose next proxy clears it (#714)
    const std::string message = GetLastErrorProxy();
    std::cout << "MESSAGE #711 SGP4 error of 33334: " << message << std::endl;
    ExpectFailureReported("");
    EXPECT_NE("", message);
    ExpectNaN(sv);
}

TEST(API, GetTLEElementsProxyOfAFailedSgp4ReturnsNaN)
{
    IO::Astrodynamics::API::DTO::TLEElementsDTO elements;
    EXPECT_NO_THROW(elements = GetTLEElementsProxy("ERR", FailedSgp4Line1, FailedSgp4Line2));
    const std::string message = GetLastErrorProxy();
    ExpectFailureReported("");
    EXPECT_NE("", message);
    EXPECT_TRUE(std::isnan(elements.Epoch));
    EXPECT_TRUE(std::isnan(elements.A));
    EXPECT_TRUE(std::isnan(elements.E));
    EXPECT_TRUE(std::isnan(elements.I));
    EXPECT_TRUE(std::isnan(elements.O));
    EXPECT_TRUE(std::isnan(elements.W));
    EXPECT_TRUE(std::isnan(elements.M));
    EXPECT_TRUE(std::isnan(elements.BalisticCoefficient));
    EXPECT_TRUE(std::isnan(elements.DragTerm));
    EXPECT_TRUE(std::isnan(elements.SecondDerivativeOfMeanMotion));
}

// conics_c signals SPICE(BADGM) for a GM that is not positive (CONICS: "The value of GM supplied was non-positive."). Before,
// the proxy did not check SPICE: it returned the state conics_c never wrote and left SPICE failed, so that the next call,
// GetCelestialBodyInfoProxy(399), returned the record of Id -1 with an empty name (measured on a77531f)
TEST(API, ConvertConicElementsToStateVectorAtEpochProxyOfANonPositiveGmReturnsNaN)
{
    IO::Astrodynamics::API::DTO::StateVectorDTO sv;
    EXPECT_NO_THROW(sv = ConvertConicElementsToStateVectorAtEpochProxy(ConicsAbout(399), 100.0, 0.0));
    const std::string message = GetLastErrorProxy();
    std::cout << "MESSAGE #711 conics_c of a GM of 0: " << message << std::endl;
    const auto earth = GetCelestialBodyInfoProxy(399);
    EXPECT_EQ(399, earth.Id);
    EXPECT_STREQ("EARTH", earth.Name);
    EXPECT_NE(std::string::npos, message.find("GM supplied was non-positive")) << message;
    ExpectNaN(sv);
    EXPECT_EQ(399, sv.centerOfMotionId);
    EXPECT_EQ(100.0, sv.epoch);
    EXPECT_STREQ("J2000", sv.inertialFrame);
    EXPECT_FALSE(TakePendingSpiceError());
}

// GALILEO ORBITER (-77), a built-in SPICE name (NAIF IDs Required Reading): the search of its centre of motion threw
// InvalidArgumentException out of the proxy. The record has the Id -1 now, as for an id SPICE does not name
TEST(API, GetCelestialBodyInfoProxyOfANamedSpacecraftReturnsNoRecord)
{
#if !defined(_WIN32)
    SpiceChar name[32]{};
    SpiceBoolean found{SPICEFALSE};
    bodc2n_c(-77, 32, name, &found);
    ASSERT_TRUE(found);
    ASSERT_STREQ("GALILEO ORBITER", name);
#endif
    IO::Astrodynamics::API::DTO::CelestialBodyDTO res;
    EXPECT_NO_THROW(res = GetCelestialBodyInfoProxy(-77));
    ExpectFailureReported("Invalid Naif Id");
    EXPECT_EQ(-1, res.Id);
    EXPECT_STREQ("", res.Name);
    EXPECT_EQ(0.0, res.GM);
}

// A site on ELARA, whose GM the kernel pool lacks: the GM read failed, then CelestialBody's SDKException, whose catch
// returned without taking SPICE's error (#712's pattern). On 46e6975 the exception depended on the unwritten found flag
TEST(API, LaunchProxyOfASiteOnABodyWithoutGmLeavesNothingPending)
{
    IO::Astrodynamics::API::DTO::LaunchDTO launch{};
    launch.launchSite.id = 507001;
    launch.launchSite.name = "ELARA_LAUNCH";
    launch.launchSite.bodyId = 507;
    launch.launchSite.directoryPath = SitePath.data();
    launch.recoverySite.id = 507002;
    launch.recoverySite.name = "ELARA_RECOVERY";
    launch.recoverySite.bodyId = 507;
    launch.recoverySite.directoryPath = SitePath.data();
    launch.targetOrbit.position = {7.0e6, 0.0, 0.0};
    launch.targetOrbit.velocity = {0.0, 7.5e3, 0.0};
    launch.targetOrbit.centerOfMotionId = 507;
    launch.targetOrbit.SetFrame("J2000");
    launch.window.start = 0.0;
    launch.window.end = 86400.0;
    bool res = true;
    EXPECT_NO_THROW(res = LaunchProxy(launch));
    EXPECT_FALSE(res);
    ExpectFailureReported("BODY507_GM");
}

// #712: a failed ReadOrientationProxy left SPICE failed (ckgp_c's error after the fallback), so that the next native call
// failed with that stale message, and reported "No orientation found" instead of SPICE's message. A CK of a native
// spacecraft (its clock written and loaded by its constructor) read in a frame SPICE does not know: false, SPICE's message
// (SPICE(UNKNOWNFRAME) of ckgpav_c --> CKGPAV --> FRMCHG, "The number 0 is not a recognized id-code for a reference frame.",
// the frame name read as the id 0; measured with CSPICE N0067), nothing pending, and the next proxy, a load, succeeds. The same CK read in J2000 first: the ordinary read is unchanged
TEST(API, ReadOrientationProxyOfAnUnknownFrameReturnsSpicesMessageAndLeavesNothingPending)
{
    auto folder = FreshFolder("ioa_712_cpp_ck");
    {
        const auto earth = std::make_shared<IO::Astrodynamics::Body::CelestialBody>(399);
        std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbit = std::make_unique<
            IO::Astrodynamics::OrbitalParameters::StateVector>(earth, IO::Astrodynamics::Math::Vector3D(6800000.0, 0.0, 0.0),
                                                               IO::Astrodynamics::Math::Vector3D(0.0, 8000.0, 0.0),
                                                               IO::Astrodynamics::Time::TDB(0.0s),
                                                               IO::Astrodynamics::Frames::InertialFrames::ICRF());
        IO::Astrodynamics::Body::Spacecraft::Spacecraft spacecraft(-1999811, "IOA712CK", 1000.0, 3000.0, folder.string(),
                                                                   std::move(orbit));
        const auto ck = folder / "o.bc";
        auto records = Records(3);
        ASSERT_TRUE(WriteOrientationProxy(ck.string().c_str(), -1999811, records.data(), 3)) << GetLastErrorProxy();
        ASSERT_TRUE(LoadKernelsProxy(ck.string().c_str())) << GetLastErrorProxy();

        IO::Astrodynamics::API::DTO::WindowDTO window{};
        window.start = 0.0;
        window.end = 2.0;
        IO::Astrodynamics::API::DTO::StateOrientationDTO so[3];
        ASSERT_TRUE(ReadOrientationProxy(window, -1999811, 0.0, "J2000", 1.0, so)) << GetLastErrorProxy();
        EXPECT_EQ(1.0, std::abs(so[2].orientation.w));

        bool res = true;
        EXPECT_NO_THROW(res = ReadOrientationProxy(window, -1999811, 0.0, "NOPE_FRAME", 1.0, so));
        EXPECT_FALSE(res);
        std::cout << "MESSAGE #712 ReadOrientationProxy in an unknown frame: " << GetLastErrorProxy() << std::endl;
        ExpectFailureReported("is not a recognized id-code for a reference frame");
        auto kernel = LoadTextKernel("ioa_712_cpp_after.tpc", "IOA712_VALUE = ( 1.0 )");
        UnloadTextKernel(kernel);
        EXPECT_TRUE(UnloadKernelsProxy(ck.string().c_str())) << GetLastErrorProxy();
    }
    std::filesystem::remove_all(folder);
}

// No clock for the spacecraft: SPICE's KERNELVARNOTFOUND of SCLK_DATA_TYPE_1999812 was reset by the fallback and lost
TEST(API, ReadOrientationProxyWithoutAClockReturnsSpicesMessage)
{
    IO::Astrodynamics::API::DTO::WindowDTO window{};
    window.start = 0.0;
    window.end = 2.0;
    IO::Astrodynamics::API::DTO::StateOrientationDTO so[3];
    bool res = true;
    EXPECT_NO_THROW(res = ReadOrientationProxy(window, -1999812, 0.0, "J2000", 1.0, so));
    EXPECT_FALSE(res);
    std::cout << "MESSAGE #712 ReadOrientationProxy without a clock: " << GetLastErrorProxy() << std::endl;
    ExpectFailureReported("SCLK_DATA_TYPE_");
    auto kernel = LoadTextKernel("ioa_712_cpp_after_clock.tpc", "IOA712_CLOCK_VALUE = ( 1.0 )");
    UnloadTextKernel(kernel);
}

// #714: lastError held the message of the last failure until another failure replaced it, so that a proxy that failed
// without a message of its own, or the .NET layer reading the message after a later successful call, reported the failure
// of another call, about another body. Each proxy that reports through lastError now clears it when it starts: after a
// failure (a conversion about ELARA, whose GM gm_de431.tpc lacks), every one of them that succeeds leaves it empty
namespace
{
    void FailAConversion()
    {
        ConvertConicElementsToStateVectorProxy(ConicsAbout(507));
        ASSERT_STRNE("", GetLastErrorProxy());
    }

    // The proxy succeeds and leaves lastError empty after a failure
    void ExpectClearedBy(const std::string &proxy, const std::function<bool()> &succeeds)
    {
        FailAConversion();
        EXPECT_TRUE(succeeds()) << proxy;
        EXPECT_STREQ("", GetLastErrorProxy()) << proxy;
    }

    bool IsFinite(const IO::Astrodynamics::API::DTO::StateVectorDTO &sv)
    {
        return std::isfinite(sv.position.x) && std::isfinite(sv.position.y) && std::isfinite(sv.position.z) &&
               std::isfinite(sv.velocity.x) && std::isfinite(sv.velocity.y) && std::isfinite(sv.velocity.z);
    }

    IO::Astrodynamics::API::DTO::StateVectorDTO EarthState()
    {
        IO::Astrodynamics::API::DTO::StateVectorDTO sv;
        sv.position = {7.0e6, 0.0, 0.0};
        sv.velocity = {0.0, 7.5e3, 0.0};
        sv.epoch = 0.0;
        sv.centerOfMotionId = 399;
        sv.SetFrame("J2000");
        return sv;
    }

    const char *const IssLine1 = "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054";
    const char *const IssLine2 = "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703";
}

TEST(API, EveryReportingProxyThatSucceedsClearsTheLastError)
{
    const double earthGm = GetCelestialBodyInfoProxy(399).GM;
    ExpectClearedBy("ConvertConicElementsToStateVectorProxy", [] { return IsFinite(ConvertConicElementsToStateVectorProxy(ConicsAbout(399))); });
    ExpectClearedBy("ConvertEquinoctialElementsToStateVectorProxy",
                    [] { return IsFinite(ConvertEquinoctialElementsToStateVectorProxy(EquinoctialAbout(399))); });
    ExpectClearedBy("ConvertConicElementsToStateVectorAtEpochProxy",
                    [earthGm] { return IsFinite(ConvertConicElementsToStateVectorAtEpochProxy(ConicsAbout(399), 100.0, earthGm)); });
    ExpectClearedBy("ConvertStateVectorToEquatorialCoordinatesProxy",
                    [] { return std::isfinite(ConvertStateVectorToEquatorialCoordinatesProxy(EarthState()).rightAscension); });
    ExpectClearedBy("ConvertStateVectorToConicOrbitalElementProxy",
                    [earthGm] { return std::isfinite(ConvertStateVectorToConicOrbitalElementProxy(EarthState(), earthGm).eccentricity); });
    ExpectClearedBy("Propagate2BodiesProxy", [earthGm] { return IsFinite(Propagate2BodiesProxy(EarthState(), earthGm, 60.0)); });
    ExpectClearedBy("ConvertTLEToStateVectorProxy", [] { return IsFinite(ConvertTLEToStateVectorProxy("ISS", IssLine1, IssLine2, 664440682.0)); });
    ExpectClearedBy("GetTLEElementsProxy", [] { return std::isfinite(GetTLEElementsProxy("ISS", IssLine1, IssLine2).Epoch); });
    ExpectClearedBy("GetCelestialBodyInfoProxy", [] { return GetCelestialBodyInfoProxy(399).Id == 399; });
    ExpectClearedBy("TransformFrameProxy", [] { return std::isfinite(TransformFrameProxy("J2000", "ITRF93", 0.0).Rotation.w); });
    ExpectClearedBy("ReadEphemerisAtGivenEpochProxy", [] { return IsFinite(ReadEphemerisAtGivenEpochProxy(0.0, 399, 301, "J2000", "NONE")); });
    ExpectClearedBy("ReadEphemerisProxy", []
    {
        IO::Astrodynamics::API::DTO::WindowDTO window{};
        window.start = 0.0;
        window.end = 60.0;
        IO::Astrodynamics::API::DTO::StateVectorDTO sv[2];
        return ReadEphemerisProxy(window, 399, 301, "J2000", "NONE", 60.0, sv);
    });
    ExpectClearedBy("GetFrameNameProxy", []
    {
        char name[33];
        return GetFrameNameProxy(1, name);
    });
    ExpectClearedBy("FindWindowsOnDistanceConstraintProxy", []
    {
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
        return FindWindowsOnDistanceConstraintProxy(SearchWindow("2007 JAN 1", "2007 JAN 2"), 399, 301, ">", 1.0, "NONE", 3600.0, windows);
    });
    ExpectClearedBy("FindWindowsOnCoordinateConstraintProxy", []
    {
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
        return FindWindowsOnCoordinateConstraintProxy(SearchWindow("2024 MAR 1", "2024 MAR 2"), 399, 301, "J2000", "RECTANGULAR", "Z", "<",
                                                      0.0, 0.0, "NONE", 3600.0, windows);
    });
    ExpectClearedBy("FindWindowsOnOccultationConstraintProxy", []
    {
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
        return FindWindowsOnOccultationConstraintProxy(SearchWindow("2001 DEC 14", "2001 DEC 15"), 399, 10, "IAU_SUN", "ELLIPSOID", 301,
                                                       "IAU_MOON", "ELLIPSOID", "ANY", "LT", 3600.0, windows);
    });
    ExpectClearedBy("FindWindowsOnIlluminationConstraintProxy", []
    {
        IO::Astrodynamics::API::DTO::WindowDTO windows[1000];
        IO::Astrodynamics::API::DTO::PlanetodeticDTO geodetic(2.2 * IO::Astrodynamics::Constants::DEG_RAD,
                                                              48.0 * IO::Astrodynamics::Constants::DEG_RAD, 0.0);
        return FindWindowsOnIlluminationConstraintProxy(SearchWindow("2021 MAY 17", "2021 MAY 18"), 10, "SUN", 399, "IAU_EARTH", geodetic,
                                                        "INCIDENCE", "<", IO::Astrodynamics::Constants::PI2, 0.0, "NONE", 3600.0,
                                                        "Ellipsoid", windows);
    });
    const auto kernel = std::filesystem::temp_directory_path() / "ioa_714_cpp_clear.tpc";
    std::ofstream(kernel.string()) << "KPL/PCK\n\\begindata\nIOA714_CLEAR = ( 1.0 )\n\\begintext\n";
    ExpectClearedBy("LoadKernelsProxy", [&kernel] { return LoadKernelsProxy(kernel.string().c_str()); });
    ExpectClearedBy("UnloadKernelsProxy", [&kernel] { return UnloadKernelsProxy(kernel.string().c_str()); });
    ExpectClearedBy("UnloadEveryLoadProxy", [&kernel] { return UnloadEveryLoadProxy(kernel.string().c_str()); });
    std::filesystem::remove(kernel);
    auto folder = FreshFolder("ioa_714_cpp_clear_spk");
    const auto spk = folder / "e.bsp";
    auto states = EarthStates(5);
    ExpectClearedBy("WriteEphemerisProxy", [&spk, &states] { return WriteEphemerisProxy(spk.string().c_str(), -1999714, states.data(), 5); });
    std::filesystem::remove_all(folder);
    EXPECT_FALSE(TakePendingSpiceError());
}
