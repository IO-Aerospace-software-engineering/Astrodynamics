/**
 * @file Spacecraft.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-06-11
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Spacecraft.h>
#include <algorithm>
#include <exception>
#include <InvalidArgumentException.h>
#include <numeric>
#include <InstrumentFrameFile.h>
#include <StringHelpers.h>

IO::SDK::Body::Spacecraft::Spacecraft::Spacecraft(const int id, const std::string &name, const double dryOperatingMass, const double maximumOperatingMass, const std::string &missionPrefix, std::unique_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> orbitalParametersAtEpoch)
	: IO::SDK::Body::Body((id >= 0 ? throw SDK::Exception::SDKException("Spacecraft must have negative id") : id), name, dryOperatingMass, std::move(orbitalParametersAtEpoch)),
	  m_missionPrefix{IO::SDK::StringHelpers::ToUpper(missionPrefix)},
	  m_filesPath{std::string(IO::SDK::Parameters::KernelsPath) + "/" + IO::SDK::StringHelpers::ToUpper(name) + "_" + IO::SDK::StringHelpers::ToUpper(m_missionPrefix)},
	  m_frame(new IO::SDK::Frames::SpacecraftFrameFile(*this)),
	  m_clockKernel(new IO::SDK::Kernels::SpacecraftClockKernel(*this, 16)),
	  m_orientationKernel(new IO::SDK::Kernels::OrientationKernel(*this)),
	  m_ephemerisKernel(new IO::SDK::Kernels::EphemerisKernel(*this)),
	  m_maximumOperatingMass{maximumOperatingMass}
{
}

std::string IO::SDK::Body::Spacecraft::Spacecraft::GetMissionPrefix() const
{
	return m_missionPrefix;
}

std::string IO::SDK::Body::Spacecraft::Spacecraft::GetFilesPath() const
{
	return m_filesPath;
}

void IO::SDK::Body::Spacecraft::Spacecraft::WriteOrientations(const std::vector<std::vector<IO::SDK::OrbitalParameters::StateOrientation>> &orientations) const
{
	m_orientationKernel->WriteOrientations(orientations);
}

IO::SDK::OrbitalParameters::StateOrientation IO::SDK::Body::Spacecraft::Spacecraft::GetOrientation(const IO::SDK::Time::TDB &epoch, const IO::SDK::Time::TimeSpan &tolerance, const IO::SDK::Frames::Frames &frame) const
{
	return m_orientationKernel->ReadStateOrientation(epoch, tolerance, frame);
}

void IO::SDK::Body::Spacecraft::Spacecraft::WriteOrientationKernelComment(const std::string &comment) const
{
	m_orientationKernel->AddComment(comment);
}

std::string IO::SDK::Body::Spacecraft::Spacecraft::ReadOrientationKernelComment() const
{
	return m_orientationKernel->ReadComment();
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> IO::SDK::Body::Spacecraft::Spacecraft::GetOrientationsCoverageWindow() const
{
	return m_orientationKernel->GetCoverageWindow();
}

const IO::SDK::Kernels::SpacecraftClockKernel &IO::SDK::Body::Spacecraft::Spacecraft::GetClock() const
{
	return *m_clockKernel;
}

void IO::SDK::Body::Spacecraft::Spacecraft::WriteEphemeris(const std::vector<OrbitalParameters::StateVector> &states) const
{
	return this->m_ephemerisKernel->WriteData(states);
}

IO::SDK::OrbitalParameters::StateVector IO::SDK::Body::Spacecraft::Spacecraft::ReadEphemeris( const IO::SDK::Frames::Frames &frame, const IO::SDK::AberrationsEnum aberration, const IO::SDK::Time::TDB &tdb,const IO::SDK::Body::CelestialBody &observer) const
{
	return this->m_ephemerisKernel->ReadStateVector(observer, frame, aberration, tdb);
}

IO::SDK::Time::Window<IO::SDK::Time::TDB> IO::SDK::Body::Spacecraft::Spacecraft::GetEphemerisCoverageWindow() const
{
	return this->m_ephemerisKernel->GetCoverageWindow();
}

void IO::SDK::Body::Spacecraft::Spacecraft::WriteEphemerisKernelComment(const std::string &comment) const
{
	this->m_ephemerisKernel->AddComment(comment);
}

std::string IO::SDK::Body::Spacecraft::Spacecraft::ReadEphemerisKernelComment() const
{
	return this->m_ephemerisKernel->ReadComment();
}

void IO::SDK::Body::Spacecraft::Spacecraft::AddCircularFOVInstrument(const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle)
{
	if (HasInstrument(id))
	{
		throw IO::SDK::Exception::InvalidArgumentException("Instrument id already exists");
	}
	m_instruments.push_back(std::unique_ptr<IO::SDK::Instruments::Instrument>(new IO::SDK::Instruments::Instrument(*this, id, name, orientation, boresight, fovRefVector, fovAngle)));
}

void IO::SDK::Body::Spacecraft::Spacecraft::AddRectangularFOVInstrument(const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle, const double crossAngle)
{
	if (HasInstrument(id))
	{
		throw IO::SDK::Exception::InvalidArgumentException("Instrument id already exists");
	}
	m_instruments.push_back(std::unique_ptr<IO::SDK::Instruments::Instrument>(new IO::SDK::Instruments::Instrument(*this, id, name, orientation, IO::SDK::Instruments::FOVShapeEnum::Rectangular, boresight, fovRefVector, fovAngle, crossAngle)));
}

void IO::SDK::Body::Spacecraft::Spacecraft::AddEllipticalFOVInstrument(const unsigned short id, const std::string &name, const IO::SDK::Math::Vector3D &orientation, const IO::SDK::Math::Vector3D &boresight, const IO::SDK::Math::Vector3D &fovRefVector, const double fovAngle, const double crossAngle)
{
	if (HasInstrument(id))
	{
		throw IO::SDK::Exception::InvalidArgumentException("Instrument id already exists");
	}
	m_instruments.push_back(std::unique_ptr<IO::SDK::Instruments::Instrument>(new IO::SDK::Instruments::Instrument(*this, id, name, orientation, IO::SDK::Instruments::FOVShapeEnum::Elliptical, boresight, fovRefVector, fovAngle, crossAngle)));
}

const IO::SDK::Instruments::Instrument *IO::SDK::Body::Spacecraft::Spacecraft::GetInstrument(const int id) const
{

	auto it = std::find_if(std::begin(m_instruments), std::end(m_instruments), [&](const std::unique_ptr<IO::SDK::Instruments::Instrument> &i) { return i->GetId() == GetId() * 1000 - id; });

	if (it == m_instruments.end())
	{
		return nullptr;
	}

	return it->get();
}

bool IO::SDK::Body::Spacecraft::Spacecraft::HasInstrument(unsigned short id)
{
	for (auto &i : m_instruments)
	{
		if (i->GetId() == GetId() * 1000 - id)
		{
			return true;
		}
	}

	return false;
}

void IO::SDK::Body::Spacecraft::Spacecraft::AddFuelTank(const std::string &serialNumber, const double capacity, const double quantity)
{
	if (std::any_of(m_fuelTanks.begin(), m_fuelTanks.end(), [&serialNumber](const std::unique_ptr<IO::SDK::Body::Spacecraft::FuelTank> &f) { return (f->GetSerialNumber() == serialNumber); }))
	{
		throw IO::SDK::Exception::InvalidArgumentException("Fuel tank with serial number " + serialNumber + " already exists");
	}

	m_fuelTanks.push_back(std::make_unique<IO::SDK::Body::Spacecraft::FuelTank>(serialNumber, *this, capacity, quantity));
}

void IO::SDK::Body::Spacecraft::Spacecraft::AddEngine(const std::string &serialNumber, const std::string &name, const std::string &fuelTankSerialNumber, const Math::Vector3D &position, const Math::Vector3D &orientation, const double isp, const double fuelFlow)
{
	if (std::any_of(m_engines.begin(), m_engines.end(), [&serialNumber](const std::unique_ptr<IO::SDK::Body::Spacecraft::Engine> &e) { return (e->GetSerialNumber() == serialNumber); }))
	{
		throw IO::SDK::Exception::InvalidArgumentException("Engine with serial number " + serialNumber + " already exists");
	}

	auto it = std::find_if(m_fuelTanks.begin(), m_fuelTanks.end(), [&fuelTankSerialNumber](const std::unique_ptr<IO::SDK::Body::Spacecraft::FuelTank> &f) { return f->GetSerialNumber() == fuelTankSerialNumber; });
	const IO::SDK::Body::Spacecraft::FuelTank &fuelTank = *it->get();

	m_engines.push_back(std::make_unique<IO::SDK::Body::Spacecraft::Engine>(serialNumber, name, fuelTank, position, orientation, isp, fuelFlow));
}

void IO::SDK::Body::Spacecraft::Spacecraft::AddPayload(const std::string &serialNumber, const std::string &name, const double mass)
{
	if (std::any_of(m_payloads.begin(), m_payloads.end(), [&serialNumber](const std::unique_ptr<IO::SDK::Body::Spacecraft::Payload> &e) { return (e->GetSerialNumber() == serialNumber); }))
	{
		throw IO::SDK::Exception::InvalidArgumentException("Payload with serial number " + serialNumber + " already exists");
	}

	m_payloads.push_back(std::make_unique<IO::SDK::Body::Spacecraft::Payload>(serialNumber, name, mass));
}

double IO::SDK::Body::Spacecraft::Spacecraft::GetMass() const
{
	auto mass = IO::SDK::Body::Body::GetMass();
	return mass + std::accumulate(m_payloads.begin(), m_payloads.end(), 0.0, [](double total, const std::unique_ptr<IO::SDK::Body::Spacecraft::Payload> &item) { return total + item->GetMass(); }) + std::accumulate(m_fuelTanks.begin(), m_fuelTanks.end(), 0.0, [](double total, const std::unique_ptr<IO::SDK::Body::Spacecraft::FuelTank> &item) { return total + item->GetQuantity(); });
}

const IO::SDK::Body::Spacecraft::Engine *IO::SDK::Body::Spacecraft::Spacecraft::GetEngine(const std::string &serialNumber) const
{
	const auto it = std::find_if(m_engines.begin(), m_engines.end(), [&serialNumber](const std::unique_ptr<IO::SDK::Body::Spacecraft::Engine> &e) { return e->GetSerialNumber() == serialNumber; });

	if (it == m_engines.end())
	{
		return nullptr;
	}

	return it->get();
}

IO::SDK::Body::Spacecraft::FuelTank *IO::SDK::Body::Spacecraft::Spacecraft::GetFueltank(const std::string &serialNumber) const
{
	const auto it = std::find_if(m_fuelTanks.begin(), m_fuelTanks.end(), [&serialNumber](const std::unique_ptr<IO::SDK::Body::Spacecraft::FuelTank> &f) { return f->GetSerialNumber() == serialNumber; });

	if (it == m_fuelTanks.end())
	{
		return nullptr;
	}

	return it->get();
}

void IO::SDK::Body::Spacecraft::Spacecraft::ReleasePayload(const std::string &serialNumber)
{
	if (serialNumber.empty())
	{
		throw IO::SDK::Exception::InvalidArgumentException("Payload serial number must be filled");
	}

	auto it = std::find_if(m_payloads.begin(), m_payloads.end(), [&serialNumber](const std::unique_ptr<IO::SDK::Body::Spacecraft::Payload> &p) { return p->GetSerialNumber() == serialNumber; });
	if (it == m_payloads.end())
	{
		throw IO::SDK::Exception::InvalidArgumentException("Invalid payload serial number");
	}

	m_payloads.erase(it);
}

// void IO::SDK::Body::Spacecraft::Spacecraft::UpdateStateVector(IO::SDK::OrbitalParameters::StateVector &newStateVector)
// {
// 	m_currentStateVector = newStateVector;
// }

double IO::SDK::Body::Spacecraft::Spacecraft::GetDryOperatingMass() const
{
	return IO::SDK::Body::Body::GetMass();
}
