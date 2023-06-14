/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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

std::string IO::Astrodynamics::Instruments::Instrument::GetFilesPath() const
{
    return m_filesPath;
}

std::string IO::Astrodynamics::Instruments::Instrument::GetName() const
{
    return m_name;
}

int IO::Astrodynamics::Instruments::Instrument::GetId() const
{
    return m_id;
}

const IO::Astrodynamics::Body::Spacecraft::Spacecraft &IO::Astrodynamics::Instruments::Instrument::GetSpacecraft() const
{
    return m_spacecraft;
}

const std::unique_ptr<IO::Astrodynamics::Frames::InstrumentFrameFile> &IO::Astrodynamics::Instruments::Instrument::GetFrame() const
{
    return m_frame;
}

IO::Astrodynamics::Instruments::Instrument::Instrument(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft,
                                             const unsigned short id, const std::string &name,
                                             const IO::Astrodynamics::Math::Vector3D &orientation,
                                             const IO::Astrodynamics::Math::Vector3D &boresight,
                                             const IO::Astrodynamics::Math::Vector3D &fovRefVector,
                                             const double fovAngle) : m_spacecraft{spacecraft},
                                                                      m_id{id < 1000 ? spacecraft.GetId() * 1000 - id
                                                                                     : throw IO::Astrodynamics::Exception::InvalidArgumentException(
                                                                                      "Instrument Id must be a positive number < 1000")},
                                                                      m_name{IO::Astrodynamics::StringHelpers::ToUpper(name)},
                                                                      m_filesPath{spacecraft.GetFilesPath() +
                                                                                  "/Instruments/" +
                                                                                  IO::Astrodynamics::StringHelpers::ToUpper(
                                                                                          name)},
                                                                      m_frame(new IO::Astrodynamics::Frames::InstrumentFrameFile(
                                                                              *this, orientation)),
                                                                      m_orientation{orientation},
                                                                      m_fovShape{
                                                                              IO::Astrodynamics::Instruments::FOVShapeEnum::Circular},
                                                                      m_boresight{boresight},
                                                                      m_fovRefVector{fovRefVector}
{

    const_cast<std::unique_ptr<IO::Astrodynamics::Kernels::InstrumentKernel> &>(m_kernel).reset(
            new IO::Astrodynamics::Kernels::CircularInstrumentKernel(*this, boresight, fovRefVector, fovAngle));
}

IO::Astrodynamics::Instruments::Instrument::Instrument(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft,
                                             const unsigned short id, const std::string &name,
                                             const IO::Astrodynamics::Math::Vector3D &orientation,
                                             const IO::Astrodynamics::Instruments::FOVShapeEnum fovShape,
                                             const IO::Astrodynamics::Math::Vector3D &boresight,
                                             const IO::Astrodynamics::Math::Vector3D &fovRefVector, const double fovAngle,
                                             const double crossAngle)
        : m_spacecraft{spacecraft},
          m_id{id < 1000 ? spacecraft.GetId() * 1000 - id : throw IO::Astrodynamics::Exception::InvalidArgumentException(
                  "Instrument Id must be a positive number < 1000")},
          m_name{IO::Astrodynamics::StringHelpers::ToUpper(name)},
          m_filesPath{spacecraft.GetFilesPath() + "/Instruments/" + IO::Astrodynamics::StringHelpers::ToUpper(name)},
          m_frame(new IO::Astrodynamics::Frames::InstrumentFrameFile(*this, orientation)),
          m_orientation{orientation},
          m_fovShape{fovShape},
          m_boresight{boresight},
          m_fovRefVector{fovRefVector}
{

    if (fovShape == IO::Astrodynamics::Instruments::FOVShapeEnum::Circular)
    {
        throw IO::Astrodynamics::Exception::SDKException("This constructor can't be used with circular field of view instrument");
    }

    if (fovShape == IO::Astrodynamics::Instruments::FOVShapeEnum::Rectangular)
    {
        const_cast<std::unique_ptr<IO::Astrodynamics::Kernels::InstrumentKernel> &>(m_kernel).reset(
                new IO::Astrodynamics::Kernels::RectangularInstrumentKernel(*this, boresight, fovRefVector, fovAngle,
                                                                  crossAngle));
    } else if (fovShape == IO::Astrodynamics::Instruments::FOVShapeEnum::Elliptical)
    {
        const_cast<std::unique_ptr<IO::Astrodynamics::Kernels::InstrumentKernel> &>(m_kernel).reset(
                new IO::Astrodynamics::Kernels::EllipticalInstrumentKernel(*this, boresight, fovRefVector, fovAngle, crossAngle));
    }
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Instruments::Instrument::GetBoresight() const
{
    return m_boresight;
}

IO::Astrodynamics::Instruments::FOVShapeEnum IO::Astrodynamics::Instruments::Instrument::GetFOVShape() const
{
    return m_fovShape;
}

std::vector<IO::Astrodynamics::Math::Vector3D> IO::Astrodynamics::Instruments::Instrument::GetFOVBoundaries() const
{
    SpiceChar shape[20];
    SpiceChar frame[50];
    SpiceDouble boresight[3];
    SpiceDouble bounds[4][3];
    SpiceInt n;

    getfov_c(m_id, 4, 20, 50, shape, frame, boresight, &n, bounds);

    std::vector<IO::Astrodynamics::Math::Vector3D> res;
    res.reserve(n);
    for (int i = 0; i < n; i++)
    {
        res.emplace_back(bounds[i][0], bounds[i][1], bounds[i][2]);
    }

    return res;
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Instruments::Instrument::FindWindowsWhereInFieldOfView(
        const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, const IO::Astrodynamics::Body::Body &targetBody,
        const IO::Astrodynamics::AberrationsEnum &aberration,
        const IO::Astrodynamics::Time::TimeSpan &stepSize
) const
{
    std::string shape{"POINT"};
    std::string frame;

    auto celestialBody = dynamic_cast<const IO::Astrodynamics::Body::CelestialBody *>(&targetBody);
    if (celestialBody)
    {
        shape = "ELLIPSOID";
        frame = celestialBody->GetBodyFixedFrame().GetName();
    }

    return IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(searchWindow, m_spacecraft.GetId(), m_id, targetBody.GetId(), frame, shape, aberration, stepSize);
}

std::vector<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB>>
IO::Astrodynamics::Instruments::Instrument::FindWindowsWhereInFieldOfView(
        const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> &searchWindow, const IO::Astrodynamics::Sites::Site &site,
        const IO::Astrodynamics::AberrationsEnum &aberration,
        const IO::Astrodynamics::Time::TimeSpan &stepSize
) const
{
    std::string shape{"POINT"};
    std::string frame;

    return IO::Astrodynamics::Constraints::GeometryFinder::FindWindowsInFieldOfViewConstraint(searchWindow, m_spacecraft.GetId(), m_id, site.GetId(), frame, shape, aberration, stepSize);
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Instruments::Instrument::GetBoresight(const IO::Astrodynamics::Frames::Frames &frame,
                                                                       const IO::Astrodynamics::Time::TDB &epoch) const
{

    double encodedClock = m_spacecraft.GetClock().ConvertToEncodedClock(epoch);
    double tolerance = m_spacecraft.GetClock().GetTicksPerSeconds(); // Tolerance of 1 second is acceptable
    SpiceDouble cmat[3][3]{};
    SpiceDouble clockOut{};
    SpiceBoolean found{};

    ckgp_c(m_spacecraft.GetFrame()->GetId(), encodedClock, tolerance, frame.ToCharArray(), cmat, &clockOut, &found);

    if (!found)
    {
        throw IO::Astrodynamics::Exception::SDKException("Insufficient data to compute boresight in frame at give epoch");
    }

    auto boresightInFrame = GetBoresightInSpacecraftFrame().Normalize();
    SpiceDouble localBoresight[3] = {boresightInFrame.GetX(), boresightInFrame.GetY(), boresightInFrame.GetZ()};
    SpiceDouble boresight[3];
    mtxv_c(cmat, localBoresight, boresight);
    return IO::Astrodynamics::Math::Vector3D{boresight[0], boresight[1], boresight[2]};
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Instruments::Instrument::GetBoresightInSpacecraftFrame() const
{
    auto q = m_boresight.To(m_orientation);
    return m_boresight.Rotate(q);
}
