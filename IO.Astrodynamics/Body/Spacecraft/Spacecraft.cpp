/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <Spacecraft.h>
#include <numeric>
#include <StringHelpers.h>
#include <Parameters.h>
#include <InvalidArgumentException.h>

IO::Astrodynamics::Body::Spacecraft::Spacecraft::Spacecraft(const int id, const std::string &name, const double dryOperatingMass, const double maximumOperatingMass,
                                                  const std::string &directoryPath, std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch)
        : Spacecraft(id, name, dryOperatingMass, maximumOperatingMass, directoryPath, std::move(orbitalParametersAtEpoch), IO::Astrodynamics::Math::Vector3D(0.0, 1.0, 0.0),
                     IO::Astrodynamics::Math::Vector3D(0.0, 0.0, 1.0))
{


}

IO::Astrodynamics::Body::Spacecraft::Spacecraft::Spacecraft(const int id, const std::string &name, const double dryOperatingMass,
                                                  double maximumOperatingMass, std::string directoryPath,
                                                  std::unique_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch,
                                                  const IO::Astrodynamics::Math::Vector3D &front,
                                                  const IO::Astrodynamics::Math::Vector3D &top) : IO::Astrodynamics::Body::Body(
        (id >= 0 ? throw IO::Astrodynamics::Exception::SDKException("Spacecraft must have negative id") : id),
        name, dryOperatingMass, std::move(orbitalParametersAtEpoch)),
                                                                                        m_filesPath{std::move(directoryPath) + "/" + name},
                                                                                        m_frame(new IO::Astrodynamics::Frames::SpacecraftFrameFile(
                                                                                                *this)),
                                                                                        m_clockKernel(
                                                                                                new IO::Astrodynamics::Kernels::SpacecraftClockKernel(
                                                                                                        *this, Parameters::ClockAccuracy)),
                                                                                        m_orientationKernel(
                                                                                                new IO::Astrodynamics::Kernels::OrientationKernel(
                                                                                                        m_filesPath + "/Orientations/" + name + ".ck", id, m_frame->m_id)),
                                                                                        m_ephemerisKernel(
                                                                                                new IO::Astrodynamics::Kernels::EphemerisKernel(
                                                                                                        m_filesPath + "/Ephemeris/" + name + ".spk", id)),
                                                                                        m_maximumOperatingMass{
                                                                                                maximumOperatingMass},
                                                                                        Top{top}, Front{front}, Right{front.CrossProduct(top)}, Bottom{top.Reverse()},
                                                                                        Back(front.Reverse()), Left{Right.Reverse()}
{
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::WriteOrientations(
        const std::vector<std::vector<IO::Astrodynamics::OrbitalParameters::StateOrientation>> &orientations) const
{
    m_orientationKernel->WriteOrientations(orientations);
}

IO::Astrodynamics::OrbitalParameters::StateOrientation
IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetOrientation(const IO::Astrodynamics::Time::TDB &epoch,
                                                      const IO::Astrodynamics::Time::TimeSpan &tolerance,
                                                      const IO::Astrodynamics::Frames::Frames &frame) const
{
    return m_orientationKernel->ReadStateOrientation(*this, epoch, tolerance, frame);
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::WriteOrientationKernelComment(const std::string &comment) const
{
    m_orientationKernel->AddComment(comment);
}

std::string IO::Astrodynamics::Body::Spacecraft::Spacecraft::ReadOrientationKernelComment() const
{
    return m_orientationKernel->ReadComment();
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetOrientationsCoverageWindow() const
{
    return m_orientationKernel->GetCoverageWindow();
}

const IO::Astrodynamics::Kernels::SpacecraftClockKernel &IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetClock() const
{
    return *m_clockKernel;
}

void
IO::Astrodynamics::Body::Spacecraft::Spacecraft::WriteEphemeris(const std::vector<OrbitalParameters::StateVector> &states) const
{
    return this->m_ephemerisKernel->WriteData(states);
}

IO::Astrodynamics::OrbitalParameters::StateVector
IO::Astrodynamics::Body::Spacecraft::Spacecraft::ReadEphemeris(const IO::Astrodynamics::Frames::Frames &frame,
                                                     const IO::Astrodynamics::AberrationsEnum aberration,
                                                     const IO::Astrodynamics::Time::TDB &tdb,
                                                     const IO::Astrodynamics::Body::CelestialBody &observer) const
{
    return this->m_ephemerisKernel->ReadStateVector(observer, frame, aberration, tdb);
}

IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::TDB> IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetEphemerisCoverageWindow() const
{
    return this->m_ephemerisKernel->GetCoverageWindow();
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::WriteEphemerisKernelComment(const std::string &comment) const
{
    this->m_ephemerisKernel->AddComment(comment);
}

std::string IO::Astrodynamics::Body::Spacecraft::Spacecraft::ReadEphemerisKernelComment() const
{
    return this->m_ephemerisKernel->ReadComment();
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::AddCircularFOVInstrument(const unsigned short id, const std::string &name,
                                                                     const IO::Astrodynamics::Math::Vector3D &orientation,
                                                                     const IO::Astrodynamics::Math::Vector3D &boresight,
                                                                     const IO::Astrodynamics::Math::Vector3D &fovRefVector,
                                                                     const double fovAngle)
{
    if (HasInstrument(id))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Instrument id already exists");
    }
    m_instruments.push_back(std::unique_ptr<IO::Astrodynamics::Instruments::Instrument>(
            new IO::Astrodynamics::Instruments::Instrument(*this, id, name, orientation, boresight, fovRefVector, fovAngle)));
}

void
IO::Astrodynamics::Body::Spacecraft::Spacecraft::AddRectangularFOVInstrument(const unsigned short id, const std::string &name,
                                                                   const IO::Astrodynamics::Math::Vector3D &orientation,
                                                                   const IO::Astrodynamics::Math::Vector3D &boresight,
                                                                   const IO::Astrodynamics::Math::Vector3D &fovRefVector,
                                                                   const double fovAngle, const double crossAngle)
{
    if (HasInstrument(id))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Instrument id already exists");
    }
    m_instruments.push_back(std::unique_ptr<IO::Astrodynamics::Instruments::Instrument>(
            new IO::Astrodynamics::Instruments::Instrument(*this, id, name, orientation,
                                                 IO::Astrodynamics::Instruments::FOVShapeEnum::Rectangular, boresight,
                                                 fovRefVector, fovAngle, crossAngle)));
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::AddEllipticalFOVInstrument(const unsigned short id, const std::string &name,
                                                                       const IO::Astrodynamics::Math::Vector3D &orientation,
                                                                       const IO::Astrodynamics::Math::Vector3D &boresight,
                                                                       const IO::Astrodynamics::Math::Vector3D &fovRefVector,
                                                                       const double fovAngle, const double crossAngle)
{
    if (HasInstrument(id))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Instrument id already exists");
    }
    m_instruments.push_back(std::unique_ptr<IO::Astrodynamics::Instruments::Instrument>(
            new IO::Astrodynamics::Instruments::Instrument(*this, id, name, orientation,
                                                 IO::Astrodynamics::Instruments::FOVShapeEnum::Elliptical, boresight,
                                                 fovRefVector, fovAngle, crossAngle)));
}

const IO::Astrodynamics::Instruments::Instrument *IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetInstrument(const int id) const
{

    auto it = std::find_if(std::begin(m_instruments), std::end(m_instruments),
                           [&](const std::unique_ptr<IO::Astrodynamics::Instruments::Instrument> &i) {
                               return i->GetId() == GetId() * 1000 - id;
                           });

    if (it == m_instruments.end())
    {
        return nullptr;
    }

    return it->get();
}

bool IO::Astrodynamics::Body::Spacecraft::Spacecraft::HasInstrument(unsigned short id)
{
    for (auto &i: m_instruments)
    {
        if (i->GetId() == GetId() * 1000 - id)
        {
            return true;
        }
    }

    return false;
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::AddFuelTank(const std::string &serialNumber, const double capacity,
                                                        const double quantity)
{
    if (std::any_of(m_fuelTanks.begin(), m_fuelTanks.end(),
                    [&serialNumber](const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::FuelTank> &f) {
                        return (f->GetSerialNumber() == serialNumber);
                    }))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException(
                "Fuel tank with serial number " + serialNumber + " already exists");
    }

    m_fuelTanks.push_back(
            std::make_unique<IO::Astrodynamics::Body::Spacecraft::FuelTank>(serialNumber, *this, capacity, quantity));
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::AddEngine(const std::string &serialNumber, const std::string &name,
                                                      const std::string &fuelTankSerialNumber,
                                                      const Math::Vector3D &position, const Math::Vector3D &orientation,
                                                      const double isp, const double fuelFlow)
{
    if (std::any_of(m_engines.begin(), m_engines.end(),
                    [&serialNumber](const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::Engine> &e) {
                        return (e->GetSerialNumber() == serialNumber);
                    }))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException(
                "Engine with serial number " + serialNumber + " already exists");
    }

    auto it = std::find_if(m_fuelTanks.begin(), m_fuelTanks.end(), [&fuelTankSerialNumber](
            const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::FuelTank> &f) {
        return f->GetSerialNumber() == fuelTankSerialNumber;
    });
    const IO::Astrodynamics::Body::Spacecraft::FuelTank &fuelTank = **it;

    m_engines.push_back(
            std::make_unique<IO::Astrodynamics::Body::Spacecraft::Engine>(serialNumber, name, fuelTank, position, orientation,
                                                                isp, fuelFlow));
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::AddPayload(const std::string &serialNumber, const std::string &name,
                                                       const double mass)
{
    if (std::any_of(m_payloads.begin(), m_payloads.end(),
                    [&serialNumber](const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::Payload> &e) {
                        return (e->GetSerialNumber() == serialNumber);
                    }))
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException(
                "Payload with serial number " + serialNumber + " already exists");
    }

    m_payloads.push_back(std::make_unique<IO::Astrodynamics::Body::Spacecraft::Payload>(serialNumber, name, mass));
}

double IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetMass() const
{
    auto mass = IO::Astrodynamics::Body::Body::GetMass();
    return mass + std::accumulate(m_payloads.begin(), m_payloads.end(), 0.0, [](double total,
                                                                                const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::Payload> &item) {
        return total + item->GetMass();
    }) + std::accumulate(m_fuelTanks.begin(), m_fuelTanks.end(), 0.0,
                         [](double total, const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::FuelTank> &item) {
                             return total + item->GetQuantity();
                         });
}

const IO::Astrodynamics::Body::Spacecraft::Engine *
IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetEngine(const std::string &serialNumber) const
{
    const auto it = std::find_if(m_engines.begin(), m_engines.end(),
                                 [&serialNumber](const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::Engine> &e) {
                                     return e->GetSerialNumber() == serialNumber;
                                 });

    if (it == m_engines.end())
    {
        return nullptr;
    }

    return it->get();
}

IO::Astrodynamics::Body::Spacecraft::FuelTank *
IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetFueltank(const std::string &serialNumber) const
{
    const auto it = std::find_if(m_fuelTanks.begin(), m_fuelTanks.end(),
                                 [&serialNumber](const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::FuelTank> &f) {
                                     return f->GetSerialNumber() == serialNumber;
                                 });

    if (it == m_fuelTanks.end())
    {
        return nullptr;
    }

    return it->get();
}

void IO::Astrodynamics::Body::Spacecraft::Spacecraft::ReleasePayload(const std::string &serialNumber)
{
    if (serialNumber.empty())
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Payload serial number must be filled");
    }

    auto it = std::find_if(m_payloads.begin(), m_payloads.end(),
                           [&serialNumber](const std::unique_ptr<IO::Astrodynamics::Body::Spacecraft::Payload> &p) {
                               return p->GetSerialNumber() == serialNumber;
                           });
    if (it == m_payloads.end())
    {
        throw IO::Astrodynamics::Exception::InvalidArgumentException("Invalid payload serial number");
    }

    m_payloads.erase(it);
}

double IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetDryOperatingMass() const
{
    return IO::Astrodynamics::Body::Body::GetMass();
}

const std::unique_ptr<IO::Astrodynamics::Frames::SpacecraftFrameFile> &IO::Astrodynamics::Body::Spacecraft::Spacecraft::GetFrame() const
{
    return this->m_frame;
}


