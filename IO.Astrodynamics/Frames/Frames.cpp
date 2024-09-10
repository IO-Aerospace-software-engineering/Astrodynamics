/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Frames.h>
#include <iostream>
#include <Quaternion.h>
#include <SpiceUsr.h>
#include <UTC.h>
#include <sofa.h>
#include <sstream>
#include <Constants.h>
#include <StringHelpers.h>

#include <utility>

IO::Astrodynamics::Frames::Frames::Frames(std::string strView) : m_name{std::move(strView)}
{
}

const char* IO::Astrodynamics::Frames::Frames::ToCharArray() const
{
    return m_name.c_str();
}

bool IO::Astrodynamics::Frames::Frames::operator==(const Frames& frame) const
{
    return m_name == frame.m_name;
}

bool IO::Astrodynamics::Frames::Frames::operator!=(const Frames& frame) const
{
    return !(m_name == frame.m_name);
}

bool IO::Astrodynamics::Frames::Frames::operator==(Frames& frame) const
{
    return m_name == frame.m_name;
}

bool IO::Astrodynamics::Frames::Frames::operator!=(Frames& frame) const
{
    return !(m_name == frame.m_name);
}

std::string IO::Astrodynamics::Frames::Frames::GetName() const
{
    return m_name;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::ToFrame6x6(
    const Frames& frame, const Time::TDB& epoch) const
{
    bool isFromTEME = IO::Astrodynamics::StringHelpers::ToUpper(m_name) == "TEME";
    bool isToTEME = IO::Astrodynamics::StringHelpers::ToUpper(frame.m_name) == "TEME";

    auto from = isFromTEME ? "ITRF93" : m_name;
    auto to = isToTEME ? "ITRF93" : frame.m_name;
    SpiceDouble sform[6][6];
    sxform_c(from.c_str(), to.c_str(), epoch.GetSecondsFromJ2000().count(), sform);

    SpiceDouble** xform;
    xform = new SpiceDouble*[6];

    for (int i = 0; i < 6; ++i)
    {
        xform[i] = new SpiceDouble[6];
    }

    for (size_t i = 0; i < 6; i++)
    {
        for (size_t j = 0; j < 6; j++)
        {
            xform[i][j] = sform[i][j];
        }
    }

    Math::Matrix mtx(6, 6, xform);


    for (int i = 0; i < 6; ++i)
    {
        delete[] xform[i];
    }

    delete[] xform;

    if (isToTEME)
    {
        return FromITRFToTEME(epoch.ToUTC()).Multiply(mtx);
    }

    if (isFromTEME)
    {
        return mtx.Multiply(FromTEMEToITRF(epoch.ToUTC()));
    }

    return mtx;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::ToFrame3x3(
    const Frames& frame, const Time::TDB& epoch) const
{
    SpiceDouble sform[3][3];
    pxform_c(this->m_name.c_str(), frame.m_name.c_str(), epoch.GetSecondsFromJ2000().count(), sform);

    SpiceDouble** xform;
    xform = new SpiceDouble*[3];

    for (int i = 0; i < 3; ++i)
    {
        xform[i] = new SpiceDouble[3];
    }

    for (size_t i = 0; i < 3; i++)
    {
        for (size_t j = 0; j < 3; j++)
        {
            xform[i][j] = sform[i][j];
        }
    }

    Math::Matrix mtx(3, 3, xform);

    for (int i = 0; i < 3; ++i)
    {
        delete[] xform[i];
    }

    delete[] xform;

    return mtx;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Frames::Frames::TransformVector(
    const Frames& to, const Math::Vector3D& vector, const Time::TDB& epoch) const
{
    auto mtx = ToFrame3x3(to, epoch);
    double v[3];
    v[0] = vector.GetX();
    v[1] = vector.GetY();
    v[2] = vector.GetZ();

    double convertedMtx[3][3];
    for (size_t i = 0; i < 3; i++)
    {
        for (size_t j = 0; j < 3; j++)
        {
            convertedMtx[i][j] = mtx.GetValue(i, j);
        }
    }

    double nstate[3];
    mxv_c(convertedMtx, v, nstate);

    return Math::Vector3D{nstate[0], nstate[1], nstate[2]};
}


IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::FromTEMEToITRF(const Time::UTC& epoch)
{
    // Extract dates
    double jd_utc1;
    double jd_utc2;
    double jd_tt1;
    double jd_tt2;
    Time::UTC::ConvertToJulianUTC_TT(epoch, jd_utc1, jd_utc2, jd_tt1, jd_tt2);


    auto gcrs = FromTEMEToGCRS(epoch);
    auto rawMtx = gcrs.GetRawData();
    double pnm[3][3];
    for (size_t i = 0; i < 3; i++)
    {
        for (size_t j = 0; j < 3; j++)
        {
            pnm[i][j] = rawMtx[i][j];
        }
    }

    // Apply apparent sideral rotation
    double gast = iauGst06(jd_utc1, jd_utc2, jd_tt1, jd_tt2, pnm);

    double gastmtx[3][3]{1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0};
    iauRz(gast, gastmtx);

    Math::Matrix transform6x6(6, 6);
    for (int i = 0; i < 3; ++i)
    {
        for (int j = 0; j < 3; ++j)
        {
            transform6x6.SetValue(i, j, gastmtx[i][j]);
            transform6x6.SetValue(i + 3, j + 3, gastmtx[i][j]); // Bottom-right block
        }
    }

    // Coriolis effect (skew-symmetric matrix for Earth's angular velocity)
    transform6x6.SetValue(3, 1, Constants::OMEGA_EARTH * gastmtx[1][1]);
    transform6x6.SetValue(3, 2, Constants::OMEGA_EARTH * gastmtx[1][2]);
    transform6x6.SetValue(4, 0, -Constants::OMEGA_EARTH * gastmtx[0][0]);
    transform6x6.SetValue(4, 2, -Constants::OMEGA_EARTH * gastmtx[0][2]);

    // Fill in the skew-symmetric matrix for Earth's angular velocity
    // Bottom-left block (Omega * R_GAST)
    transform6x6.SetValue(3, 0, Constants::OMEGA_EARTH * gastmtx[1][0]);
    transform6x6.SetValue(4, 1, -Constants::OMEGA_EARTH * gastmtx[0][1]);


    //    Math::Matrix pnmGastMtx{gastmtx};
    auto str = transform6x6.ToString();
    return transform6x6;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::FromITRFToTEME(const Time::UTC& epoch)
{
    // Extract dates
    double jd_utc1;
    double jd_utc2;
    double jd_tt1;
    double jd_tt2;
    Time::UTC::ConvertToJulianUTC_TT(epoch, jd_utc1, jd_utc2, jd_tt1, jd_tt2);


    auto gcrs = FromTEMEToGCRS(epoch);
    auto rawMtx = gcrs.GetRawData();
    double pnm[3][3];
    for (size_t i = 0; i < 3; i++)
    {
        for (size_t j = 0; j < 3; j++)
        {
            pnm[i][j] = rawMtx[i][j];
        }
    }

    // Apply apparent sideral rotation
    double gast = iauGst06(jd_utc1, jd_utc2, jd_tt1, jd_tt2, pnm);

    double gastmtx[3][3]{1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0};
    iauRz(-gast, gastmtx);

    Math::Matrix transform6x6(6, 6);
    for (int i = 0; i < 3; ++i)
    {
        for (int j = 0; j < 3; ++j)
        {
            transform6x6.SetValue(i, j, gastmtx[i][j]);
            transform6x6.SetValue(i + 3, j + 3, gastmtx[i][j]); // Bottom-right block
        }
    }

    // Coriolis effect (skew-symmetric matrix for Earth's angular velocity)
    transform6x6.SetValue(3, 1, -Constants::OMEGA_EARTH * gastmtx[1][1]);
    transform6x6.SetValue(3, 2, -Constants::OMEGA_EARTH * gastmtx[1][2]);
    transform6x6.SetValue(4, 0, Constants::OMEGA_EARTH * gastmtx[0][0]);
    transform6x6.SetValue(4, 2, Constants::OMEGA_EARTH * gastmtx[0][2]);

    // Fill in the skew-symmetric matrix for Earth's angular velocity
    // Bottom-left block (Omega * R_GAST)
    transform6x6.SetValue(3, 0, -Constants::OMEGA_EARTH * gastmtx[1][0]);
    transform6x6.SetValue(4, 1, Constants::OMEGA_EARTH * gastmtx[0][1]);


    //    Math::Matrix pnmGastMtx{gastmtx};
    auto str = transform6x6.ToString();
    return transform6x6;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::PolarMotion(const Time::UTC& epoch)
{
    // Extract dates
    double jd_utc1;
    double jd_utc2;
    double jd_tt1;
    double jd_tt2;
    Time::UTC::ConvertToJulianUTC_TT(epoch, jd_utc1, jd_utc2, jd_tt1, jd_tt2);

    double x, y, s;
    iauXys06a(jd_tt1, jd_tt2, &x, &y, &s);
    double rpom[3][3];
    iauPom00(x, y, s, rpom);
    Math::Matrix POMMtx(rpom);
    return POMMtx;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::FromTEMEToGCRS(const Time::UTC& epoch)
{
    // Extract dates
    double jd_utc1;
    double jd_utc2;
    double jd_tt1;
    double jd_tt2;
    Time::UTC::ConvertToJulianUTC_TT(epoch, jd_utc1, jd_utc2, jd_tt1, jd_tt2);

    // Apply precession-nutation -> GCRS
    double pnm[3][3];
    iauPnm06a(jd_tt1, jd_tt2, pnm);

    Math::Matrix pnmMtx(pnm);
    return pnmMtx;
}
