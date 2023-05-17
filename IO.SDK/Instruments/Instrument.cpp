/**
 * @file Instrument.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Instrument.h>
#include <CircularInstrumentKernel.h>
#include <InstrumentFrameFile.h>
#include <SDKException.h>
#include <InvalidArgumentException.h>
#include <EllipticalInstrumentKernel.h>
#include <RectangularInstrumentKernel.h>
#include <SpiceUsr.h>
#include <StringHelpers.h>
#include <GeometryFinder.h>

std::string IO::SDK::Instruments::Instrument::GetFilesPath() const
{
    return m_filesPath;
}

std::string IO::SDK::Instruments::Instrument::GetName() const
{
    return m_name;
}

int IO::SDK::Instruments::Instrument::GetId() const
{
    return m_id;
}

const IO::SDK::Body::Spacecraft::Spacecraft &IO::SDK::Instruments::Instrument::GetSpacecraft() const
{
    return m_spacecraft;
}

const std::unique_ptr<IO::SDK::Frames::InstrumentFrameFile> &IO::SDK::Instruments::Instrument::GetFrame() const
{
    return m_frame;
}

IO::SDK::Instruments::Instrument::Instrument(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft,
                                             const unsigned short id, const std::string &name,
                                             const IO::SDK::Math::Vector3D &orientation,
                                             const IO::SDK::Math::Vector3D &boresight,
                                             const IO::SDK::Math::Vector3D &fovRefVector,
                                             const double fovAngle) : m_spacecraft{spacecraft},
                                                                      m_id{id < 1000 ? spacecraft.GetId() * 1000 - id
                                                                                     : throw IO::SDK::Exception::InvalidArgumentException(
                                                                                      "Instrument Id must be a positive number < 1000")},
                                                                      m_name{IO::SDK::StringHelpers::ToUpper(name)},
                                                                      m_filesPath{spacecraft.GetFilesPath() +
                                                                                  "/Instruments/" +
                                                                                  IO::SDK::StringHelpers::ToUpper(
                                                                                          name)},
                                                                      m_frame(new IO::SDK::Frames::InstrumentFrameFile(
                                                                              *this, orientation)),
                                                                      m_orientation{orientation},
                                                                      m_fovShape{
                                                                              IO::SDK::Instruments::FOVShapeEnum::Circular},
                                                                      m_boresight{boresight},
                                                                      m_fovRefVector{fovRefVector}
{

    const_cast<std::unique_ptr<IO::SDK::Kernels::InstrumentKernel> &>(m_kernel).reset(
            new IO::SDK::Kernels::CircularInstrumentKernel(*this, boresight, fovRefVector, fovAngle));
}

IO::SDK::Instruments::Instrument::Instrument(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft,
                                             const unsigned short id, const std::string &name,
                                             const IO::SDK::Math::Vector3D &orientation,
                                             const IO::SDK::Instruments::FOVShapeEnum fovShape,
                                             const IO::SDK::Math::Vector3D &boresight,
                                             const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle,
                                             const double crossAngle)
        : m_spacecraft{spacecraft},
          m_id{id < 1000 ? spacecraft.GetId() * 1000 - id : throw IO::SDK::Exception::InvalidArgumentException(
                  "Instrument Id must be a positive number < 1000")},
          m_name{IO::SDK::StringHelpers::ToUpper(name)},
          m_filesPath{spacecraft.GetFilesPath() + "/Instruments/" + IO::SDK::StringHelpers::ToUpper(name)},
          m_frame(new IO::SDK::Frames::InstrumentFrameFile(*this, orientation)),
          m_orientation{orientation},
          m_fovShape{fovShape},
          m_boresight{boresight},
          m_fovRefVector{fovRefVector}
{

    if (fovShape == IO::SDK::Instruments::FOVShapeEnum::Circular)
    {
        throw IO::SDK::Exception::SDKException("This constructor can't be used with circular field of view instrument");
    }

    if (fovShape == IO::SDK::Instruments::FOVShapeEnum::Rectangular)
    {
        const_cast<std::unique_ptr<IO::SDK::Kernels::InstrumentKernel> &>(m_kernel).reset(
                new IO::SDK::Kernels::RectangularInstrumentKernel(*this, boresight, fovRefVector, fovAngle,
                                                                  crossAngle));
    } else if (fovShape == IO::SDK::Instruments::FOVShapeEnum::Elliptical)
    {
        const_cast<std::unique_ptr<IO::SDK::Kernels::InstrumentKernel> &>(m_kernel).reset(
                new IO::SDK::Kernels::EllipticalInstrumentKernel(*this, boresight, fovRefVector, fovAngle, crossAngle));
    }
}

IO::SDK::Math::Vector3D IO::SDK::Instruments::Instrument::GetBoresight() const
{
    return m_boresight;
}

IO::SDK::Instruments::FOVShapeEnum IO::SDK::Instruments::Instrument::GetFOVShape() const
{
    return m_fovShape;
}

std::vector<IO::SDK::Math::Vector3D> IO::SDK::Instruments::Instrument::GetFOVBoundaries() const
{
    SpiceChar shape[20];
    SpiceChar frame[50];
    SpiceDouble boresight[3];
    SpiceDouble bounds[4][3];
    SpiceInt n;

    getfov_c(m_id, 4, 20, 50, shape, frame, boresight, &n, bounds);

    std::vector<IO::SDK::Math::Vector3D> res;
    res.reserve(n);
    for (int i = 0; i < n; i++)
    {
        res.emplace_back(bounds[i][0], bounds[i][1], bounds[i][2]);
    }

    return res;
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Instruments::Instrument::FindWindowsWhereInFieldOfView(
        const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const IO::SDK::Body::Body &targetBody,
        const IO::SDK::AberrationsEnum &aberration,
        const IO::SDK::Time::TimeSpan &stepSize
) const
{
    std::string shape{"POINT"};
    std::string frame;

    auto celestialBody = dynamic_cast<const IO::SDK::Body::CelestialBody *>(&targetBody);
    if (celestialBody)
    {
        shape = "ELLIPSOID";
        frame = celestialBody->GetBodyFixedFrame().GetName();
    }

    return IO::SDK::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(searchWindow, m_spacecraft.GetId(), m_id, targetBody.GetId(), frame, shape, aberration, stepSize);
}

std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>
IO::SDK::Instruments::Instrument::FindWindowsWhereInFieldOfView(
        const IO::SDK::Time::Window<IO::SDK::Time::TDB> &searchWindow, const IO::SDK::Sites::Site &site,
        const IO::SDK::AberrationsEnum &aberration,
        const IO::SDK::Time::TimeSpan &stepSize
) const
{
    std::string shape{"POINT"};
    std::string frame;

    return IO::SDK::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(searchWindow, m_spacecraft.GetId(), m_id, site.GetId(), frame, shape, aberration, stepSize);
}

IO::SDK::Math::Vector3D IO::SDK::Instruments::Instrument::GetBoresight(const IO::SDK::Frames::Frames &frame,
                                                                       const IO::SDK::Time::TDB &epoch) const
{

    double encodedClock = m_spacecraft.GetClock().ConvertToEncodedClock(epoch);
    double tolerance = m_spacecraft.GetClock().GetTicksPerSeconds(); // Tolerance of 1 second is acceptable
    SpiceDouble cmat[3][3]{};
    SpiceDouble clockOut{};
    SpiceBoolean found{};

    ckgp_c(m_spacecraft.GetFrame()->GetId(), encodedClock, tolerance, frame.ToCharArray(), cmat, &clockOut, &found);

    if (!found)
    {
        throw IO::SDK::Exception::SDKException("Insufficient data to compute boresight in frame at give epoch");
    }

    auto boresightInFrame = GetBoresightInSpacecraftFrame().Normalize();
    SpiceDouble localBoresight[3] = {boresightInFrame.GetX(), boresightInFrame.GetY(), boresightInFrame.GetZ()};
    SpiceDouble boresight[3];
    mtxv_c(cmat, localBoresight, boresight);
    return IO::SDK::Math::Vector3D{boresight[0], boresight[1], boresight[2]};
}

IO::SDK::Math::Vector3D IO::SDK::Instruments::Instrument::GetBoresightInSpacecraftFrame() const
{
    auto q = IO::SDK::Math::Quaternion(m_orientation.Normalize(), m_orientation.Magnitude());
    return m_boresight.Rotate(q);
}
