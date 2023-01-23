//
// Created by usr on 20/01/23.
//

#include <string>

static const std::string ckTemplate =
        "KPL/FK\n"
        "\\begindata\n"
        "FRAME_{framename}_SPACECRAFT   = {frameid}\n"
        "FRAME_{frameid}_NAME      = '{framename}_SPACECRAFT'\n"
        "FRAME_{frameid}_CLASS     =  3\n"
        "FRAME_{frameid}_CLASS_ID  = {frameid}\n"
        "FRAME_{frameid}_CENTER    = {spacecraftid}\n"
        "CK_{frameid}_SCLK         = {spacecraftid}\n"
        "CK_{frameid}_SPK          = {spacecraftid}\n"
        "OBJECT_{spacecraftid}_FRAME       = '{framename}_SPACECRAFT'\n"
        "NAIF_BODY_NAME              += '{framename}_SPACECRAFT'\n"
        "NAIF_BODY_CODE              += {frameid}\n"
        "NAIF_BODY_NAME              += '{spacecraftname}'\n"
        "NAIF_BODY_CODE              += {spacecraftid}\n"
        "\\begintext\n";

static const std::string IKCircular =
        "KPL/IK\n"
        "\\begindata\n"
        "INS{instrumentid}_FOV_CLASS_SPEC       = 'ANGLES'\n"
        "INS{instrumentid}_FOV_SHAPE            = 'CIRCLE'\n"
        "INS{instrumentid}_FOV_FRAME            = '{framename}'\n"
        "INS{instrumentid}_BORESIGHT            = ( {bx}, {by}, {bz} )\n"
        "INS{instrumentid}_FOV_REF_VECTOR       = ( {rx}, {ry}, {rz} )\n"
        "INS{instrumentid}_FOV_REF_ANGLE        = {angle}\n"
        "INS{instrumentid}_FOV_ANGLE_UNITS      = 'RADIANS'\n"
        "\\begintext\n";

static const std::string IKEliptical =
        "KPL/IK\n"
        "\\begindata\n"
        "INS{instrumentid}_FOV_CLASS_SPEC       = 'ANGLES'\n"
        "INS{instrumentid}_FOV_SHAPE            = 'ELLIPSE'\n"
        "INS{instrumentid}_FOV_FRAME            = '{framename}'\n"
        "INS{instrumentid}_BORESIGHT            = ( {bx}, {by}, {bz} )\n"
        "INS{instrumentid}_FOV_REF_VECTOR       = ( {rx}, {ry}, {rz} )\n"
        "INS{instrumentid}_FOV_REF_ANGLE        = {angle}\n"
        "INS{instrumentid}_FOV_CROSS_ANGLE      = {cangle}\n"
        "INS{instrumentid}_FOV_ANGLE_UNITS      = 'RADIANS'\n"
        "\\begintext\n";

static const std::string IKRectangular =
"KPL/IK\n"
"\\begindata\n"
"INS{instrumentid}_FOV_CLASS_SPEC       = 'ANGLES'\n"
"INS{instrumentid}_FOV_SHAPE            = 'RECTANGLE'\n"
"INS{instrumentid}_FOV_FRAME            = '{framename}'\n"
"INS{instrumentid}_BORESIGHT            = ( {bx}, {by}, {bz} )\n"
"INS{instrumentid}_FOV_REF_VECTOR       = ( {rx}, {ry}, {rz} )\n"
"INS{instrumentid}_FOV_REF_ANGLE        = {angle}\n"
"INS{instrumentid}_FOV_CROSS_ANGLE      = {cangle}\n"
"INS{instrumentid}_FOV_ANGLE_UNITS      = 'RADIANS'\n"
"\\begintext\n";

//Generic clock used only in pre mission design
//It's generated as 2021-01-01/00:00:00.0 UTC ans is equal to 6.6273126918393E+08 TDB past seconds
//Resolution : 15.259 us
static const std::string Sclk =
"KPL/SCLK\n"
"\\begindata\n"
"SCLK_KERNEL_ID           = ( @2021-01-01/00:00:00.0 )\n"
"SCLK_DATA_TYPE_{id}        = ( 1 )\n"
"SCLK01_TIME_SYSTEM_{id}    = ( 1 )\n"
"SCLK01_N_FIELDS_{id}       = ( 2 )\n"
"SCLK01_MODULI_{id}         = ( 4294967296 {resolution} )\n"
"SCLK01_OFFSETS_{id}        = ( 0 0 )\n"
"SCLK01_OUTPUT_DELIM_{id}   = ( 2 )\n"
"SCLK_PARTITION_START_{id}  = ( 0.0000000000000E+00 )\n"
"SCLK_PARTITION_END_{id}    = ( 2.8147497671065E+14 )\n"
"SCLK01_COEFFICIENTS_{id}   = ( 0.0000000000000E+00     6.62731200000000E+08     1.0000000000000E+00 )\n"
"\\begintext\n";