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

#include <utility>

IO::Astrodynamics::Frames::Frames::Frames(std::string strView) : m_name{std::move(strView)}
{
}

const char *IO::Astrodynamics::Frames::Frames::ToCharArray() const
{
    return m_name.c_str();
}

bool IO::Astrodynamics::Frames::Frames::operator==(const IO::Astrodynamics::Frames::Frames &frame) const
{
    return m_name == frame.m_name;
}

bool IO::Astrodynamics::Frames::Frames::operator!=(const IO::Astrodynamics::Frames::Frames &frame) const
{
    return !(m_name == frame.m_name);
}

bool IO::Astrodynamics::Frames::Frames::operator==(IO::Astrodynamics::Frames::Frames &frame) const
{
    return m_name == frame.m_name;
}

bool IO::Astrodynamics::Frames::Frames::operator!=(IO::Astrodynamics::Frames::Frames &frame) const
{
    return !(m_name == frame.m_name);
}

std::string IO::Astrodynamics::Frames::Frames::GetName() const
{
    return m_name;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::ToFrame6x6(
        const Frames &frame, const IO::Astrodynamics::Time::TDB &epoch) const
{
    SpiceDouble sform[6][6];
    sxform_c(this->m_name.c_str(), frame.m_name.c_str(), epoch.GetSecondsFromJ2000().count(), sform);

    SpiceDouble **xform;
    xform = new SpiceDouble *[6];

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

    IO::Astrodynamics::Math::Matrix mtx(6, 6, xform);

    for (int i = 0; i < 6; ++i)
    {
        delete[] xform[i];
    }

    delete[] xform;

    return mtx;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::ToFrame3x3(
        const Frames &frame, const IO::Astrodynamics::Time::TDB &epoch) const
{
    SpiceDouble sform[3][3];
    pxform_c(this->m_name.c_str(), frame.m_name.c_str(), epoch.GetSecondsFromJ2000().count(), sform);

    SpiceDouble **xform;
    xform = new SpiceDouble *[3];

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

    IO::Astrodynamics::Math::Matrix mtx(3, 3, xform);

    for (int i = 0; i < 3; ++i)
    {
        delete[] xform[i];
    }

    delete[] xform;

    return mtx;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Frames::Frames::TransformVector(
        const Frames &to, const IO::Astrodynamics::Math::Vector3D &vector, const IO::Astrodynamics::Time::TDB &epoch) const
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

    return IO::Astrodynamics::Math::Vector3D{nstate[0], nstate[1], nstate[2]};
}

void IO::Astrodynamics::Frames::Frames::ConvertToJulianUTC_TT(const IO::Astrodynamics::Time::TDB &epoch,
                                                              double &jd_utc1, double &jd_utc2, double &jd_tt1,
                                                              double &jd_tt2)
{
    const auto utc = epoch.ToUTC().ToString();
    int year, month, day, hour, minute;
    double second;
    ExtractDateTimeComponents(utc, year, month, day, hour, minute, second);

    // Variables pour les Julian Dates
    double jd_tai1, jd_tai2;

    // Convertir la date UTC en Julian Date (jd_utc1 et jd_utc2)
    iauDtf2d("UTC", year, month, day, hour, minute, second, &jd_utc1, &jd_utc2);

    // Convertir UTC en TAI
    iauUtctai(jd_utc1, jd_utc2, &jd_tai1, &jd_tai2);

    // Convertir TAI en TT
    iauTaitt(jd_tai1, jd_tai2, &jd_tt1, &jd_tt2);
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::ToITRF(
        const IO::Astrodynamics::Time::TDB &epoch)
{
    // Extract dates
    double jd_utc1;
    double jd_utc2;
    double jd_tt1;
    double jd_tt2;
    ConvertToJulianUTC_TT(epoch, jd_utc1, jd_utc2, jd_tt1, jd_tt2);


     auto gcrs = ToGCRS(epoch);
     auto rawMtx = gcrs.GetRawData();
     double pnm[3][3];
     for (size_t i = 0; i < 3; i++)
     {
         for (size_t j = 0; j < 3; j++)
         {
             pnm[i][j] = rawMtx[i][j];
         }
     }
    // Rotation sidérale apparente pour obtenir le vecteur TEME
    double gast = iauGst06(jd_utc1, jd_utc2, jd_tt1, jd_tt2,pnm);

    double gastmtx[3][3]{1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0};
    iauRz(gast, gastmtx);

    double x, y, s;
    iauXys06a(jd_tt1, jd_tt2, &x, &y, &s);
    double pomMtx[3][3]{1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0};
    //iauPom00(-x, -y, s, pomMtx);

    double res[3][3];
    mtxm_c(pomMtx, gastmtx, res);
    Math::Matrix pnmGastMtx{res};
    return pnmGastMtx;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::PolarMotion(
        const IO::Astrodynamics::Time::TDB &epoch)
{
    // Extract dates
    double jd_utc1;
    double jd_utc2;
    double jd_tt1;
    double jd_tt2;
    ConvertToJulianUTC_TT(epoch, jd_utc1, jd_utc2, jd_tt1, jd_tt2);

    double x, y, s;
    iauXys06a(jd_tt1, jd_tt2, &x, &y, &s);
    double rpom[3][3];
    iauPom00(-x, -y, s, rpom);
    Math::Matrix pnmGastMtx(rpom);
    return pnmGastMtx;
}

IO::Astrodynamics::Math::Matrix IO::Astrodynamics::Frames::Frames::ToGCRS(const IO::Astrodynamics::Time::TDB &epoch)
{
    // Extract dates
    double jd_utc1;
    double jd_utc2;
    double jd_tt1;
    double jd_tt2;
    ConvertToJulianUTC_TT(epoch, jd_utc1, jd_utc2, jd_tt1, jd_tt2);

    // Apply precession-nutation -> GCRS
    double pnm[3][3];
    iauPnm06a(jd_tt1, jd_tt2, pnm);

    Math::Matrix pnmMtx(pnm);
    return pnmMtx;
}

void IO::Astrodynamics::Frames::Frames::ExtractDateTimeComponents(const std::string &dateTimeStr,
                                                                  int &year, int &month, int &day,
                                                                  int &hour, int &minute, double &second)
{
    // DateTime format: YYYY-MM-DD HR:MN:SC.###### (UTC)
    std::istringstream iss(dateTimeStr);
    char delim; // Used to ignore the delimiters (i.e., '-', ':', and spaces)

    // Parse the date (YYYY-MM-DD)
    iss >> year >> delim >> month >> delim >> day;

    // Parse the time (HR:MN:SC.######)
    iss >> hour >> delim >> minute >> delim >> second;
}
