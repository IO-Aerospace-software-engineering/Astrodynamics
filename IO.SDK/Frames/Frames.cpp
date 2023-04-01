/**
 * @file Frames.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Frames.h>
#include <SpiceUsr.h>

#include <utility>

IO::SDK::Frames::Frames::Frames(std::string strView) : m_name{std::move(strView)}
{
}

const char *IO::SDK::Frames::Frames::ToCharArray() const
{
    return m_name.c_str();
}

bool IO::SDK::Frames::Frames::operator==(const IO::SDK::Frames::Frames &frame) const
{
    return m_name == frame.m_name;
}

bool IO::SDK::Frames::Frames::operator!=(const IO::SDK::Frames::Frames &frame) const
{
    return !(m_name == frame.m_name);
}

bool IO::SDK::Frames::Frames::operator==(IO::SDK::Frames::Frames &frame) const
{
    return m_name == frame.m_name;
}

bool IO::SDK::Frames::Frames::operator!=(IO::SDK::Frames::Frames &frame) const
{
    return !(m_name == frame.m_name);
}

std::string IO::SDK::Frames::Frames::GetName() const
{
    return m_name;
}

IO::SDK::Math::Matrix IO::SDK::Frames::Frames::ToFrame6x6(const Frames &frame, const IO::SDK::Time::TDB &epoch) const
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

    IO::SDK::Math::Matrix mtx(6, 6, xform);

    for (int i = 0; i < 6; ++i)
    {
        delete[] xform[i];
    }

    delete[] xform;

    return mtx;
}

IO::SDK::Math::Matrix IO::SDK::Frames::Frames::ToFrame3x3(const Frames &frame, const IO::SDK::Time::TDB &epoch) const
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

    IO::SDK::Math::Matrix mtx(3, 3, xform);

    for (int i = 0; i < 3; ++i)
    {
        delete[] xform[i];
    }

    delete[] xform;

    return mtx;
}

IO::SDK::Math::Vector3D IO::SDK::Frames::Frames::TransformVector(const Frames &to, const IO::SDK::Math::Vector3D &vector,const IO::SDK::Time::TDB &epoch) const
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

    return IO::SDK::Math::Vector3D{nstate[0], nstate[1], nstate[2]};
}