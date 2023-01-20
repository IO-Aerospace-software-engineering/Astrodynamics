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